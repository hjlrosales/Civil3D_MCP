#!/usr/bin/env node
/**
 * Generates GitHub-style release notes for a version.
 *
 * Output combines:
 *   1. the matching entry from CHANGELOG.md (keep-a-changelog format), and
 *   2. the git commit log since the previous `v*` tag.
 *
 * Writes to artifacts/release-notes/<version>.md and prints the result.
 *
 * Usage:
 *   node eng/scripts/release-notes.mjs                 # use the version in eng/version.json
 *   node eng/scripts/release-notes.mjs 1.0.0-rc.1
 */
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const changelogPath = path.join(root, 'CHANGELOG.md');

function currentVersion() {
  const meta = JSON.parse(fs.readFileSync(path.join(root, 'eng', 'version.json'), 'utf8'));
  return meta.version;
}

function extractChangelogEntry(version) {
  if (!fs.existsSync(changelogPath)) return null;
  const text = fs.readFileSync(changelogPath, 'utf8');
  const re = new RegExp(`## \\[${version.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\]`, 'm');
  const start = text.search(re);
  if (start < 0) return null;
  const next = text.indexOf('\n## ', start + 4);
  const section = next >= 0 ? text.slice(start, next) : text.slice(start);
  return section.trim();
}

function gitLogSinceLastTag() {
  try {
    const tags = execFileSync('git', ['tag', '--list', 'v*', '--sort=-version:refname'], { cwd: root })
      .toString().trim().split('\n').filter(Boolean);
    const since = tags.length > 0 ? `${tags[0]}..HEAD` : 'HEAD';
    const out = execFileSync('git', ['log', since, '--oneline', '--no-merges'], { cwd: root })
      .toString().trim();
    return out.length > 0 ? out : '(no commits)';
  } catch {
    return '(no git history available)';
  }
}

const version = process.argv[2] ?? currentVersion();
const changelog = extractChangelogEntry(version);
const commits = gitLogSinceLastTag();

const notes = [
  `# Autodesk MCP Platform ${version}`,
  '',
  changelog ?? `> No CHANGELOG.md entry found for ${version}.`,
  '',
  '## Commits since the last tag',
  '',
  '```',
  commits,
  '```',
  '',
].join('\n');

const outDir = path.join(root, 'artifacts', 'release-notes');
fs.mkdirSync(outDir, { recursive: true });
const outFile = path.join(outDir, `${version}.md`);
fs.writeFileSync(outFile, notes + '\n', 'utf8');
console.log(notes);
console.log(`\nWrote ${path.relative(root, outFile)}`);
