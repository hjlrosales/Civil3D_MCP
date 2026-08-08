#!/usr/bin/env node
/**
 * Produces the release artifact manifest: filename, version, SHA-256, size,
 * build timestamp and platform info for every release artifact.
 *
 * No machine-specific paths are emitted - only the artifact file name.
 *
 * Usage:
 *   node eng/scripts/release-manifest.mjs <version>                # default artifact locations
 *   node eng/scripts/release-manifest.mjs <version> --bundle <path> --server <path>
 *                                                                  # explicit artifact files
 *   node eng/scripts/release-manifest.mjs <version> --bundle-dir <dir> --server-dir <dir>
 *                                                                  # scan dirs for the expected names
 *
 * Also writes SHA256SUMS next to the manifest (same data, `hash  filename` lines).
 */
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const version = args[0];
if (version === undefined) {
  console.error('Usage: node eng/scripts/release-manifest.mjs <version> [--bundle <path>] [--server <path>]');
  process.exit(1);
}

function flagValue(name) {
  const flag = args.find((a) => a.startsWith(name + '='));
  return flag !== undefined ? flag.slice(name.length + 1) : undefined;
}

const bundleFlag = flagValue('--bundle');
const serverFlag = flagValue('--server');
const bundleDirFlag = flagValue('--bundle-dir');
const serverDirFlag = flagValue('--server-dir');

function resolveCandidate(value) {
  return path.isAbsolute(value) ? value : path.resolve(root, value);
}

function findInDir(dir, name) {
  if (dir === undefined) return undefined;
  const resolved = path.isAbsolute(dir) ? dir : path.resolve(root, dir);
  if (!fs.existsSync(resolved)) return undefined;
  const direct = path.join(resolved, name);
  if (fs.existsSync(direct)) return direct;
  // The artifact may sit one level down (e.g. the bridge zip inside its folder).
  for (const entry of fs.readdirSync(resolved)) {
    const candidate = path.join(resolved, entry, name);
    if (fs.existsSync(candidate)) return candidate;
  }
  return undefined;
}

const bundleName = `Civil3D.Bridge.Bundle-${version}.zip`;
const serverName = `autodesk-mcp-server-${version}.tgz`;

const bundleFile =
  bundleFlag !== undefined ? resolveCandidate(bundleFlag) :
  findInDir(bundleDirFlag, bundleName) ?? path.join(root, 'artifacts', 'bundles', bundleName);
const serverFile =
  serverFlag !== undefined ? resolveCandidate(serverFlag) :
  findInDir(serverDirFlag, serverName) ?? path.join(root, 'artifacts', 'packages', serverName);

const manifest = {
  version,
  generatedUtc: new Date().toISOString(),
  generatedOn: `${process.platform} ${os.arch()} (node ${process.version})`,
  artifacts: [],
};

let failed = false;
for (const [name, file] of [[bundleName, bundleFile], [serverName, serverFile]]) {
  if (!fs.existsSync(file)) {
    console.error(`Missing artifact: ${name} (looked at ${file})`);
    failed = true;
    continue;
  }
  const data = fs.readFileSync(file);
  manifest.artifacts.push({
    filename: name,
    version,
    sha256: createHash('sha256').update(data).digest('hex'),
    sizeBytes: data.length,
    buildUtc: new Date(fs.statSync(file).mtime).toISOString(),
  });
}

fs.mkdirSync(path.join(root, 'artifacts'), { recursive: true });
const outFile = path.join(root, 'artifacts', 'release-manifest.json');
fs.writeFileSync(outFile, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
fs.writeFileSync(path.join(root, 'artifacts', 'SHA256SUMS'),
  manifest.artifacts.map((a) => `${a.sha256}  ${a.filename}`).join('\n') + '\n', 'utf8');

console.log(`Release manifest written: artifacts/release-manifest.json (version ${version})`);
for (const artifact of manifest.artifacts) {
  console.log(`  ${artifact.filename}  ${artifact.sha256.slice(0, 16)}...  ${artifact.sizeBytes} bytes`);
}
if (failed) process.exit(1);
