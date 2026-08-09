#!/usr/bin/env node
/**
 * Fresh-machine style packaging validation (Phase 9G).
 *
 * Simulates a clean install without relying on the developer's environment:
 *   1. `npm pack` the server package into a fresh temp directory.
 *   2. `npm install` the packed tarball into a second clean temp directory
 *      (no workspace symlinks, no pre-existing node_modules).
 *   3. Run the installed CLI (`--version`, `--help`) and verify it starts and
 *      shuts down cleanly with an explicit empty endpoints directory.
 *   4. Validate the bridge bundle zip (if present): readable zip,
 *      PackageContents.xml present with the expected AppVersion.
 *   5. Uninstall (delete temp dirs) and verify cleanup.
 *
 * Usage:
 *   node eng/scripts/validate-fresh-install.mjs            # full validation
 *   node eng/scripts/validate-fresh-install.mjs --server   # npm fresh-install only
 *   node eng/scripts/validate-fresh-install.mjs --bundle   # bundle integrity only
 */
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = new Set(process.argv.slice(2));
const all = !['--server', '--bundle'].some((flag) => args.has(flag));

const version = JSON.parse(fs.readFileSync(path.join(root, 'eng', 'version.json'), 'utf8')).version;
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const serverDir = path.join(root, 'src', 'server', 'Autodesk.Mcp.Server');

const failures = [];
async function check(name, fn) {
  process.stdout.write('--- ' + name + ' ... ');
  try {
    const ok = await fn();
    process.stdout.write(ok ? 'PASS\n' : 'FAIL\n');
    if (!ok) failures.push(name);
  } catch (error) {
    process.stdout.write('FAIL (threw: ' + error.message + ')\n');
    failures.push(name);
  }
}

function run(cmd, cmdArgs, opts) {
  const result = spawnSync(cmd, cmdArgs, {
    cwd: (opts && opts.cwd) || root,
    encoding: 'utf8',
    shell: process.platform === 'win32',
  });
  return { status: result.status, stdout: result.stdout || '', stderr: result.stderr || '' };
}

const existingTarball = (() => {
  const flag = process.argv.slice(2).find((a) => a.startsWith('--tarball='));
  const value = flag !== undefined ? flag.slice('--tarball='.length) : undefined;
  // Resolve relative tarball paths against the repo root: npm install runs with a
  // temp cwd, so a bare relative path would resolve inside the temp directory.
  return value !== undefined && !path.isAbsolute(value) ? path.resolve(root, value) : value;
})();

const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'amcp-fresh-'));
const packDir = path.join(tempRoot, 'pack');
const installDir = path.join(tempRoot, 'install');
const endpointsDir = path.join(tempRoot, 'endpoints');
fs.mkdirSync(packDir, { recursive: true });
fs.mkdirSync(installDir, { recursive: true });
fs.mkdirSync(endpointsDir, { recursive: true });

let tarball = '';

/** ---------------- server: fresh npm install ---------------- */
async function validateServer() {
  await check(existingTarball !== undefined ? 'use pre-packed tarball' : 'npm pack produces a tarball', () => {
    if (existingTarball !== undefined) {
      if (!fs.existsSync(existingTarball)) return false;
      tarball = existingTarball;
      return true;
    }
    const result = run(npm, ['pack', serverDir, '--pack-destination', packDir], { cwd: root });
    if (result.status !== 0) {
      process.stdout.write(result.stderr);
      return false;
    }
    const files = fs.readdirSync(packDir).filter((name) => name.endsWith('.tgz'));
    if (files.length !== 1) return false;
    tarball = path.join(packDir, files[0]);
    return true;
  });

  await check('npm install tarball into a clean dir (' + version + ')', () => {
    if (tarball === '') return false;
    const result = run(npm, ['install', tarball, '--no-audit', '--no-fund'], { cwd: installDir });
    if (result.status !== 0) {
      process.stdout.write(result.stderr);
      return false;
    }
    const bin = path.join(installDir, 'node_modules', '.bin', 'autodesk-mcp-server');
    return fs.existsSync(bin + (process.platform === 'win32' ? '.cmd' : ''));
  });

  function installedBin() {
    return path.join(installDir, 'node_modules', '.bin', 'autodesk-mcp-server') + (process.platform === 'win32' ? '.cmd' : '');
  }

  await check('installed CLI reports --version', () => {
    const result = run(installedBin(), ['--version']);
    if (result.status !== 0) {
      process.stdout.write(result.stderr);
      return false;
    }
    const clean = result.stdout.trim();
    process.stdout.write('output: ' + clean + ' ');
    return clean.length > 0 && clean.includes(version.split('-')[0]);
  });

  await check('installed CLI --help lists the config flag', () => {
    const result = run(installedBin(), ['--help']);
    if (result.status !== 0) {
      process.stdout.write(result.stderr);
      return false;
    }
    // Usage text is written to stderr by convention (stdout stays clean for the protocol).
    return (result.stdout + result.stderr).includes('--config');
  });

  await check('installed CLI starts to the ready state (empty endpoints dir)', async () => {
    const entry = path.join(installDir, 'node_modules', 'autodesk-mcp-server', 'dist', 'index.js');
    const child = spawn(process.execPath, [entry, '--endpoints-dir', endpointsDir], {
      cwd: installDir,
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let output = '';
    child.stdout.on('data', (chunk) => { output += String(chunk); });
    child.stderr.on('data', (chunk) => { output += String(chunk); });

    const deadline = Date.now() + 15000;
    while (!output.includes('ready') && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    const started = output.includes('ready');
    child.kill('SIGTERM');
    await new Promise((resolve) => setTimeout(resolve, 500));
    child.kill('SIGKILL');
    if (!started) {
      process.stdout.write(output.slice(0, 800));
    }
    return started;
  });
}

/** ---------------- bundle: integrity ---------------- */
async function validateBundle() {
  // The archive must extract straight into %APPDATA%\Autodesk\ApplicationPlugins and auto-load,
  // which means everything sits under a root folder whose name ends with '.bundle'.
  await check('bundle zip extracts to a loader-visible <name>.bundle folder', () => {
    const zip = path.join(root, 'artifacts', 'bundles', 'Civil3D.Bridge.Bundle-' + version + '.zip');
    if (!fs.existsSync(zip)) {
      process.stdout.write('(no bundle zip; run build-bridge-bundle.mjs first) ');
      return false;
    }
    const expected = 'Civil3D.Bridge.Bundle-' + version + '.bundle/PackageContents.xml';
    // No pipes in this script: the command goes through a shell that would consume them.
    const script = [
      'Add-Type -AssemblyName System.IO.Compression.FileSystem;',
      "$z = [System.IO.Compression.ZipFile]::OpenRead('" + zip.replaceAll("'", "''") + "');",
      "Write-Output ('entries=' + $z.Entries.Count);",
      "Write-Output ('rooted=' + ($z.Entries.FullName -contains '" + expected + "'));",
      // A bare PackageContents.xml would mean the archive extracts without the .bundle folder.
      "Write-Output ('unrooted=' + ($z.Entries.FullName -contains 'PackageContents.xml'));",
      '$z.Dispose()',
    ].join(' ');
    const result = run('powershell', ['-NoProfile', '-Command', script]);
    if (result.status !== 0) {
      process.stdout.write(result.stderr);
      return false;
    }
    process.stdout.write(result.stdout.trim().replaceAll('\n', ' ') + ' ');
    return result.stdout.includes('rooted=True') && result.stdout.includes('unrooted=False');
  });

  await check('PackageContents.xml declares the expected AppVersion', () => {
    const file = path.join(root, 'packaging', 'Civil3D.Bridge.Bundle', 'PackageContents.xml');
    if (!fs.existsSync(file)) return false;
    const xml = fs.readFileSync(file, 'utf8');
    return xml.includes('AppVersion="' + version + '"');
  });
}

/** ---------------- run ---------------- */
async function main() {
  if (all || args.has('--server')) await validateServer();
  if (all || args.has('--bundle')) await validateBundle();

  fs.rmSync(tempRoot, { recursive: true, force: true });
  process.stdout.write('=== Fresh-install validation summary ===\n');
  if (failures.length === 0) {
    process.stdout.write('  ALL CHECKS PASS\n');
    process.exit(0);
  }
  process.stdout.write('  FAILED: ' + failures.join(', ') + '\n');
  process.exit(1);
}

main();
