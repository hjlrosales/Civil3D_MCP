#!/usr/bin/env node
/**
 * Syncs the release version across the entire repository.
 *
 * eng/version.json is the single source of truth. Running this script rewrites:
 *   - Directory.Build.props                 (<Version>, <AssemblyVersion>, <FileVersion>, <InformationalVersion>)
 *   - package.json                          (root orchestration package)
 *   - src/server/Autodesk.Mcp.Server/package.json
 *   - packaging/Civil3D.Bridge.Bundle/PackageContents.xml  (AppVersion)
 *   - src/bridges/Civil3D.Bridge/Configuration/bridge.config.json (bridgeVersion)
 *   - examples/config/bridge.config.json    (bridgeVersion)
 *   - examples/config/server.config.json    (clientVersion)
 *
 * Usage:
 *   node eng/scripts/sync-version.mjs                 # read version from eng/version.json
 *   node eng/scripts/sync-version.mjs 1.2.3-beta.1    # bump eng/version.json and sync
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const versionFile = path.join(root, 'eng', 'version.json');

function loadVersionFile() {
  return JSON.parse(fs.readFileSync(versionFile, 'utf8'));
}

function isSemVer(text) {
  return /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$/.test(text);
}

function numericCore(full) {
  return full.split('-')[0].split('+')[0];
}

/** Replaces the value of `<key>...</key>` style XML property lines in a props file. */
function syncProps(file, pairs) {
  if (!fs.existsSync(file)) return false;
  let text = fs.readFileSync(file, 'utf8');
  for (const [key, value] of Object.entries(pairs)) {
    const re = new RegExp(`(<${key}>)([^<]*)(</${key}>)`, 'g');
    if (!re.test(text)) {
      throw new Error(`${file}: no <${key}> property found to sync.`);
    }
    text = text.replace(re, `$1${value}$3`);
  }
  fs.writeFileSync(file, text, 'utf8');
  return true;
}

/** Replaces the JSON `"key": value` for a string value. */
function syncJsonString(file, key, value) {
  if (!fs.existsSync(file)) return false;
  let text = fs.readFileSync(file, 'utf8');
  const re = new RegExp(`("${key}"\\s*:\\s*")[^"]*(")`);
  if (!re.test(text)) {
    throw new Error(`${file}: no "${key}" key found to sync.`);
  }
  text = text.replace(re, `$1${value}$2`);
  fs.writeFileSync(file, text, 'utf8');
  return true;
}

/** Replaces AppVersion="..." (and version="...") attributes in PackageContents.xml. */
function syncPackageContents(file, full, numeric) {
  if (!fs.existsSync(file)) return false;
  let text = fs.readFileSync(file, 'utf8');
  text = text.replace(/AppVersion="[^"]*"/, `AppVersion="${full}"`);
  text = text.replace(/Version="[^"]*"/, `Version="${numeric}"`);
  fs.writeFileSync(file, text, 'utf8');
  return true;
}

const next = process.argv[2];
if (next !== undefined) {
  if (!isSemVer(next)) {
    console.error(`Invalid semantic version: '${next}'`);
    process.exit(1);
  }
  const meta = loadVersionFile();
  meta.version = next;
  fs.writeFileSync(versionFile, JSON.stringify(meta, null, 2) + '\n', 'utf8');
}

const { version } = loadVersionFile();
const numeric = numericCore(version);

const updated = [];
const log = (name) => updated.push(name);

syncProps(path.join(root, 'Directory.Build.props'), {
  Version: version,
  AssemblyVersion: numeric,
  FileVersion: numeric,
  InformationalVersion: version,
}) && log('Directory.Build.props');

syncJsonString(path.join(root, 'package.json'), 'version', version) && log('package.json (root)');
syncJsonString(path.join(root, 'src', 'server', 'Autodesk.Mcp.Server', 'package.json'), 'version', version)
  && log('src/server/Autodesk.Mcp.Server/package.json');
syncPackageContents(path.join(root, 'packaging', 'Civil3D.Bridge.Bundle', 'PackageContents.xml'), version, numeric)
  && log('packaging/Civil3D.Bridge.Bundle/PackageContents.xml');

for (const rel of [
  'src/bridges/Civil3D.Bridge/Configuration/bridge.config.json',
  'examples/config/bridge.config.json',
]) {
  syncJsonString(path.join(root, rel), 'bridgeVersion', version) && log(rel);
}
syncJsonString(path.join(root, 'examples/config/server.config.json'), 'clientVersion', version)
  && log('examples/config/server.config.json');

console.log(`Synced version ${version} (assembly ${numeric}) across:`);
for (const name of updated) console.log(`  - ${name}`);
if (updated.length === 0) {
  console.log('  (nothing to sync - target files not present yet)');
}
