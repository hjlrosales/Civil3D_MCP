#!/usr/bin/env node
/**
 * Produces the release artifact manifest: filename, version, SHA-256, size,
 * build timestamp and platform info for every release artifact.
 *
 * No machine-specific paths are emitted - only the artifact file name.
 *
 * Usage:
 *   node eng/scripts/release-manifest.mjs            # default: artifacts for eng/version.json
 *   node eng/scripts/release-manifest.mjs <version>  # explicit version (e.g. 1.0.0)
 */
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const versionArg = process.argv[2];
const version = versionArg ?? JSON.parse(fs.readFileSync(path.join(root, 'eng', 'version.json'), 'utf8')).version;

const artifacts = [
  { name: `Civil3D.Bridge.Bundle-${version}.zip`, relative: path.join('artifacts', 'bundles', `Civil3D.Bridge.Bundle-${version}.zip`) },
  { name: `autodesk-mcp-server-${version}.tgz`, relative: path.join('artifacts', 'packages', `autodesk-mcp-server-${version}.tgz`) },
];

const manifest = {
  version,
  generatedUtc: new Date().toISOString(),
  platform: `${process.platform} ${os.arch()}`,
  node: process.version,
  artifacts: [],
};

let failed = false;
for (const artifact of artifacts) {
  const file = path.join(root, artifact.relative);
  if (!fs.existsSync(file)) {
    console.error(`Missing artifact: ${artifact.name}`);
    failed = true;
    continue;
  }
  const data = fs.readFileSync(file);
  manifest.artifacts.push({
    filename: artifact.name,
    version,
    sha256: createHash('sha256').update(data).digest('hex'),
    sizeBytes: data.length,
    buildUtc: new Date(fs.statSync(file).mtime).toISOString(),
  });
}

const outFile = path.join(root, 'artifacts', 'release-manifest.json');
fs.mkdirSync(path.dirname(outFile), { recursive: true });
fs.writeFileSync(outFile, JSON.stringify(manifest, null, 2) + '\n', 'utf8');

console.log(`Release manifest written: artifacts/release-manifest.json (version ${version})`);
for (const artifact of manifest.artifacts) {
  console.log(`  ${artifact.filename}  ${artifact.sha256.slice(0, 16)}...  ${artifact.sizeBytes} bytes`);
}
if (failed) process.exit(1);
