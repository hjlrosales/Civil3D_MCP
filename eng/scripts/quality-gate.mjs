#!/usr/bin/env node
/**
 * Local quality gate mirroring `.github/workflows/ci.yml`.
 *
 * Usage:
 *   node eng/scripts/quality-gate.mjs            # full gate (install + build + test + lint + pack)
 *   node eng/scripts/quality-gate.mjs --node     # node/server checks only
 *   node eng/scripts/quality-gate.mjs --dotnet   # .NET core checks (+ bridge when the SDK exists)
 *   node eng/scripts/quality-gate.mjs --e2e      # end-to-end suite only
 *   node eng/scripts/quality-gate.mjs --verify-only   # version-drift check only (fast)
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const flags = new Set(args);
const all = !['--node', '--dotnet', '--e2e', '--verify-only'].some((f) => flags.has(f));

const results = [];
function step(name, fn) {
  process.stdout.write(`\n=== ${name} ===\n`);
  try {
    const ok = fn();
    results.push({ name, ok });
    if (!ok) process.exitCode = 1;
  } catch (error) {
    process.stdout.write(`FAILED: ${error.message}\n`);
    results.push({ name, ok: false });
    process.exitCode = 1;
  }
}

function run(cmd, cmdArgs, opts = {}) {
  const result = spawnSync(cmd, cmdArgs, {
    cwd: opts.cwd ?? root,
    stdio: 'inherit',
    shell: process.platform === 'win32',
  });
  return result.status === 0;
}

const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const serverDir = path.join('src', 'server', 'Autodesk.Mcp.Server');

/** ---------------- version drift ---------------- */
function checkVersionDrift() {
  const version = JSON.parse(fs.readFileSync(path.join(root, 'eng', 'version.json'), 'utf8')).version;
  const numeric = version.split('-')[0].split('+')[0];
  const problems = [];

  const props = fs.readFileSync(path.join(root, 'Directory.Build.props'), 'utf8');
  if (!props.includes(`<Version>${version}</Version>`)) problems.push('Directory.Build.props <Version>');
  if (!props.includes(`<AssemblyVersion>${numeric}</AssemblyVersion>`)) problems.push('Directory.Build.props <AssemblyVersion>');
  if (!props.includes(`<InformationalVersion>${version}</InformationalVersion>`)) problems.push('Directory.Build.props <InformationalVersion>');

  const rootPkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));
  if (rootPkg.version !== version) problems.push('package.json (root)');
  const serverPkg = JSON.parse(fs.readFileSync(path.join(root, serverDir, 'package.json'), 'utf8'));
  if (serverPkg.version !== version) problems.push('server package.json');

  const bundle = path.join(root, 'packaging', 'Civil3D.Bridge.Bundle', 'PackageContents.xml');
  if (fs.existsSync(bundle)) {
    const xml = fs.readFileSync(bundle, 'utf8');
    if (!xml.includes(`AppVersion="${version}"`)) problems.push('PackageContents.xml AppVersion');
  }

  for (const rel of [
    'src/bridges/Civil3D.Bridge/Configuration/bridge.config.json',
    'examples/config/bridge.config.json',
  ]) {
    const file = path.join(root, rel);
    if (fs.existsSync(file)) {
      const text = fs.readFileSync(file, 'utf8');
      if (!text.includes(`"bridgeVersion": "${version}"`)) problems.push(`${rel} bridgeVersion`);
    }
  }
  const serverConfig = path.join(root, 'examples/config/server.config.json');
  if (fs.existsSync(serverConfig)) {
    const text = fs.readFileSync(serverConfig, 'utf8');
    if (!text.includes(`"clientVersion": "${version}"`)) problems.push('examples/config/server.config.json clientVersion');
  }

  if (problems.length > 0) {
    process.stdout.write(`Version drift for ${version}:\n  - ${problems.join('\n  - ')}\n`);
    return false;
  }
  process.stdout.write(`Versions in sync at ${version}.\n`);
  return true;
}

/** ---------------- node/server ---------------- */
function nodeGate() {
  const steps = [
    ['npm ci (server)', () => run(npm, ['ci'], { cwd: path.join(root, serverDir) })],
    ['typecheck', () => run(npm, ['run', 'typecheck'], { cwd: path.join(root, serverDir) })],
    ['lint', () => run(npm, ['run', 'lint'], { cwd: path.join(root, serverDir) })],
    ['test', () => run(npm, ['test'], { cwd: path.join(root, serverDir) })],
    ['build', () => run(npm, ['run', 'build'], { cwd: path.join(root, serverDir) })],
    ['pack', () => {
      fs.mkdirSync(path.join(root, 'artifacts', 'packages'), { recursive: true });
      return run(npm, ['pack', './src/server/Autodesk.Mcp.Server', '--pack-destination', 'artifacts/packages']);
    }],
    ['fresh-install validation', () => {
      // Pick the tarball for the version under test. artifacts/packages accumulates tarballs
      // from earlier releases, and taking the first entry validated a stale one.
      const version = JSON.parse(fs.readFileSync(path.join(root, 'eng', 'version.json'), 'utf8')).version;
      const expected = `autodesk-mcp-server-${version}.tgz`;
      const packages = path.join(root, 'artifacts', 'packages');
      const packed = fs.readdirSync(packages).find((name) => name === expected);
      if (packed === undefined) {
        process.stdout.write(`Expected ${expected} in artifacts/packages (run the pack step first).\n`);
        return false;
      }
      const tarball = path.join(packages, packed);
      return run('node', ['eng/scripts/validate-fresh-install.mjs', '--server', '--tarball=' + tarball]);
    }],
  ];
  for (const [name, fn] of steps) {
    if (!runStep(name, fn)) return false;
  }
  return true;
}

function runStep(name, fn) {
  process.stdout.write(`\n--- ${name} ---\n`);
  const ok = fn();
  results.push({ name, ok });
  if (!ok) process.exitCode = 1;
  return ok;
}

/** ---------------- dotnet ---------------- */
function dotnetGate() {
  if (!runStep('dotnet build (core)', () => run('dotnet', ['build', 'AutodeskMcp.Core.slnx', '-c', 'Release', '--nologo']))) {
    return false;
  }
  runStep('dotnet test (core)', () => run('dotnet', ['test', 'AutodeskMcp.Core.slnx', '-c', 'Release', '--nologo', '--no-build']));
  runStep('dotnet format (verify)', () => run('dotnet', ['format', 'AutodeskMcp.Core.slnx', '--verify-no-changes', '--no-restore']));

  // Full build incl. the bridge when the Autodesk SDK is present.
  const acad = path.join(process.env.ProgramFiles ?? 'C:\Program Files', 'Autodesk', 'AutoCAD 2025', 'acmgd.dll');
  if (fs.existsSync(acad)) {
    runStep('dotnet build (full incl. bridge)', () => run('dotnet', ['build', 'AutodeskMcp.slnx', '-c', 'Release', '--nologo']));
    runStep('dotnet test (full)', () => run('dotnet', ['test', 'AutodeskMcp.slnx', '-c', 'Release', '--nologo']));
    runStep('bridge bundle', () => run('node', ['eng/scripts/build-bridge-bundle.mjs', '--no-zip']));
  } else {
    process.stdout.write('\n(Autodesk SDK not found - skipping bridge build/tests/bundle.)\n');
  }
  return true;
}

/** ---------------- e2e ---------------- */
function e2eGate() {
  const e2eDir = path.join(root, 'e2e');
  if (!fs.existsSync(path.join(root, serverDir, 'dist', 'index.js'))) {
    if (!runStep('build server (for e2e)', () => run(npm, ['run', 'build'], { cwd: path.join(root, serverDir) }))) return false;
  }
  if (!fs.existsSync(path.join(e2eDir, 'node_modules'))) {
    if (!runStep('npm ci (e2e)', () => run(npm, ['ci'], { cwd: e2eDir }))) return false;
  }
  return runStep('e2e suite', () => run(npm, ['test'], { cwd: e2eDir }));
}

/** ---------------- run ---------------- */
if (all || flags.has('--verify-only')) {
  step('version drift check', checkVersionDrift);
}
if (all || flags.has('--node')) step('node gate', nodeGate);
if (all || flags.has('--dotnet')) step('dotnet gate', dotnetGate);
if (all || flags.has('--e2e')) step('e2e gate', e2eGate);

process.stdout.write('\n=== Quality gate summary ===\n');
for (const { name, ok } of results) {
  process.stdout.write(`  [${ok ? 'PASS' : 'FAIL'}] ${name}\n`);
}
process.exit(process.exitCode ?? 0);
