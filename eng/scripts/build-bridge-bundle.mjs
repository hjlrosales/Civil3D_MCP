#!/usr/bin/env node
/**
 * Builds the Civil3D.Bridge plugin and assembles an Autodesk Application Bundle.
 *
 * Produces:
 *   artifacts/bridge-publish/<version>/          raw dotnet publish output
 *   artifacts/bundles/Civil3D.Bridge.Bundle-<version>/
 *       PackageContents.xml
 *       Contents/                                 (dlls, docs, Configuration/)
 *   artifacts/bundles/Civil3D.Bridge.Bundle-<version>.zip
 *
 * Options:
 *   --install    additionally copy the bundle into %APPDATA%\Autodesk\ApplicationPlugins
 *                (per-user install; the loader picks it up on the next Civil 3D start)
 *   --no-zip     skip creating the zip archive
 *
 * Requires Windows with the Autodesk SDK present (AutoCAD 2025 default, override
 * with the AutodeskAcadDir MSBuild property via --msbuild "-p:AutodeskAcadDir=...").
 */
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, copyFileSync, cpSync, rmSync, writeFileSync, statSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const { version } = JSON.parse(readFileSync(path.join(root, 'eng', 'version.json'), 'utf8'));

const args = process.argv.slice(2);
const install = args.includes('--install');
const noZip = args.includes('--no-zip');
const msbuildArg = args.find((a) => a.startsWith('--msbuild'));
const extraMsbuild = msbuildArg !== undefined ? msbuildArg.slice('--msbuild'.length).trim() : '';

const bridgeProject = path.join(root, 'src', 'bridges', 'Civil3D.Bridge', 'Civil3D.Bridge.csproj');
const template = path.join(root, 'packaging', 'Civil3D.Bridge.Bundle', 'PackageContents.xml');
const publishDir = path.join(root, 'artifacts', 'bridge-publish', version);
const bundleDir = path.join(root, 'artifacts', 'bundles', `Civil3D.Bridge.Bundle-${version}`);
const zipPath = path.join(root, 'artifacts', 'bundles', `Civil3D.Bridge.Bundle-${version}.zip`);

if (!existsSync(template)) {
  console.error(`Missing bundle template: ${path.relative(root, template)}`);
  process.exit(1);
}

console.log(`[bundle] version ${version}`);
console.log(`[bundle] publishing ${path.relative(root, bridgeProject)}`);

rmSync(publishDir, { recursive: true, force: true });
const publishArgs = [
  'publish', bridgeProject,
  '-c', 'Release',
  '-o', publishDir,
  '--nologo',
];
if (extraMsbuild.length > 0) publishArgs.push(...extraMsbuild.split(/\s+/));
execFileSync('dotnet', publishArgs, { cwd: root, stdio: 'inherit' });

console.log('[bundle] assembling bundle folder');
rmSync(bundleDir, { recursive: true, force: true });
mkdirSync(path.join(bundleDir, 'Contents'), { recursive: true });

copyFileSync(template, path.join(bundleDir, 'PackageContents.xml'));

// Ship the license alongside the bundle so the plugin distribution carries it.
const licenseFile = path.join(root, 'LICENSE');
if (existsSync(licenseFile)) {
  copyFileSync(licenseFile, path.join(bundleDir, 'LICENSE'));
  console.log('[bundle] included LICENSE (MIT) at bundle root');
} else {
  console.warn('[bundle] LICENSE not found at repo root; skipping');
}

const excluded = new Set(['.pdb', '.xml']); // debug symbols + XML doc files are development-only
for (const entry of readdirSync(publishDir)) {
  const source = path.join(publishDir, entry);
  if (statSync(source).isDirectory()) {
    continue; // directories (for example Configuration/) are copied below
  }
  if (excluded.has(path.extname(entry).toLowerCase())) {
    console.log('[bundle] skipping development-only file: ' + entry);
    continue;
  }
  copyFileSync(source, path.join(bundleDir, 'Contents', entry));
}
// Keep the Configuration folder layout the loader expects.
if (existsSync(path.join(publishDir, 'Configuration'))) {
  cpSync(path.join(publishDir, 'Configuration'), path.join(bundleDir, 'Contents', 'Configuration'), { recursive: true });
}

const contents = readdirSync(path.join(bundleDir, 'Contents'))
  .filter((name) => name.endsWith('.dll'))
  .sort();
console.log(`[bundle] ${contents.length} managed assemblies in Contents/`);

if (!noZip) {
  console.log('[bundle] zipping bundle');
  rmSync(zipPath, { force: true });
  createZip(bundleDir, zipPath);
  const kb = Math.round(statSync(zipPath).size / 1024);
  console.log(`[bundle] wrote ${path.relative(root, zipPath)} (${kb} KB)`);
}

if (install) {
  installToApplicationPlugins(bundleDir, version);
}

console.log(`[bundle] done: ${path.relative(root, bundleDir)}`);
for (const name of contents) console.log(`  Contents/${name}`);
if (existsSync(path.join(bundleDir, 'Contents', 'Configuration', 'bridge.config.json'))) {
  console.log('  Contents/Configuration/bridge.config.json');
}

/** Installs the bundle into the per-user Autodesk loader directory. */
function installToApplicationPlugins(sourceDir, bundleVersion) {
  const appData = process.env.APPDATA;
  if (!appData) {
    console.warn('[bundle] %APPDATA% not set; skipping install.');
    return;
  }
  // The Autodesk ApplicationPlugins loader only discovers folders whose name ends with
  // '.bundle', so the installed folder must carry that suffix even though the build artifact
  // folder and zip keep the plain `Civil3D.Bridge.Bundle-<version>` name.
  const target = path.join(appData, 'Autodesk', 'ApplicationPlugins', `Civil3D.Bridge.Bundle-${bundleVersion}.bundle`);
  console.log(`[bundle] installing to ${target}`);
  rmSync(target, { recursive: true, force: true });
  cpSync(sourceDir, target, { recursive: true });
  console.log('[bundle] installed. Restart Civil 3D for the loader to pick it up.');
}

/**
 * Minimal STORE-method ZIP writer (no external dependencies). Bundle payloads are
 * already-compressed DLLs/XML, so STORE is acceptable and keeps the script portable.
 */
function createZip(dir, outZip) {
  const files = collectFiles(dir);
  const parts = [];
  const central = [];
  let offset = 0;

  for (const file of files) {
    const rel = path.relative(dir, file).split(path.sep).join('/');
    const data = readFileSync(file);
    const nameBuffer = Buffer.from(rel, 'utf8');
    const crc = crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0, 6);
    local.writeUInt16LE(0, 8);
    local.writeUInt16LE(0, 10);
    local.writeUInt16LE(0x21, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuffer.length, 26);
    local.writeUInt16LE(0, 28);

    parts.push(local, nameBuffer, data);

    const centralHeader = Buffer.alloc(46);
    centralHeader.writeUInt32LE(0x02014b50, 0);
    centralHeader.writeUInt16LE(20, 4);
    centralHeader.writeUInt16LE(20, 6);
    centralHeader.writeUInt16LE(0, 8);
    centralHeader.writeUInt16LE(0, 10);
    centralHeader.writeUInt16LE(0, 12);
    centralHeader.writeUInt16LE(0x21, 14);
    centralHeader.writeUInt32LE(crc, 16);
    centralHeader.writeUInt32LE(data.length, 20);
    centralHeader.writeUInt32LE(data.length, 24);
    centralHeader.writeUInt16LE(nameBuffer.length, 28);
    centralHeader.writeUInt32LE(offset, 42);

    central.push(centralHeader, nameBuffer);
    offset += 30 + nameBuffer.length + data.length;
  }

  const centralOffset = offset;
  const centralSize = central.reduce((sum, buf) => sum + buf.length, 0);

  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(0, 4);
  end.writeUInt16LE(0, 6);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(centralSize, 12);
  end.writeUInt32LE(centralOffset, 16);
  end.writeUInt16LE(0, 20);

  writeFileSync(outZip, Buffer.concat([...parts, ...central, end]));
}

function collectFiles(dir) {
  const out = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...collectFiles(full));
    else out.push(full);
  }
  return out;
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (let i = 0; i < buffer.length; i += 1) {
    crc ^= buffer[i];
    for (let bit = 0; bit < 8; bit += 1) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}
