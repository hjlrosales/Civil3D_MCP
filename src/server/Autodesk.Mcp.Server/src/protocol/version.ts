/**
 * Semantic version (SemVer 2.0.0) helpers mirroring the C# VersionInformation contract:
 * wire form is the compact string major.minor.patch[-pre-release][+build]; two-part core
 * versions are tolerated on read (1.2 is treated as 1.2.0); comparison ignores build metadata.
 */

export interface SemVer {
  major: number;
  minor: number;
  patch: number;
  /** Pre-release component (for example beta.1), or empty when absent. */
  preRelease: string;
  /** Build metadata component (for example sha.abc123), or empty when absent. */
  buildMetadata: string;
}

/** An all-zero version, used as a not-provided default on the wire. */
export const EMPTY_VERSION: SemVer = { major: 0, minor: 0, patch: 0, preRelease: '', buildMetadata: '' };

const IDENTIFIER_RE = /^[0-9A-Za-z-]+$/;

function isValidIdentifierSet(value: string): boolean {
  if (value.length === 0) {
    return false;
  }
  return value.split('.').every((id) => id.length > 0 && IDENTIFIER_RE.test(id));
}

/**
 * Parses a semantic version string. Accepts two- and three-part core versions (1.2 is treated
 * as 1.2.0) for tolerance. Returns null when the value is not a valid semantic version.
 */
export function tryParseVersion(value: string | null | undefined): SemVer | null {
  if (value === null || value === undefined || value.trim().length === 0) {
    return null;
  }

  let text = value.trim();

  let buildMetadata = '';
  const plusIndex = text.indexOf('+');
  if (plusIndex >= 0) {
    buildMetadata = text.slice(plusIndex + 1);
    text = text.slice(0, plusIndex);
  }

  let preRelease = '';
  const dashIndex = text.indexOf('-');
  let core = text;
  if (dashIndex >= 0) {
    preRelease = text.slice(dashIndex + 1);
    core = text.slice(0, dashIndex);
  }
  if (dashIndex >= 0 && preRelease.length === 0) {
    return null; // A trailing dash with an empty pre-release is invalid.
  }

  const parts = core.split('.');
  if (parts.length < 1 || parts.length > 3) {
    return null;
  }

  const major = Number.parseInt(parts[0] ?? '', 10);
  if (!Number.isInteger(major) || major < 0 || String(major) !== (parts[0] ?? '')) {
    return null;
  }
  const minorText = parts[1];
  const minor = minorText === undefined ? 0 : Number.parseInt(minorText, 10);
  if (minorText !== undefined && (!Number.isInteger(minor) || minor < 0 || String(minor) !== minorText)) {
    return null;
  }
  const patchText = parts[2];
  const patch = patchText === undefined ? 0 : Number.parseInt(patchText, 10);
  if (patchText !== undefined && (!Number.isInteger(patch) || patch < 0 || String(patch) !== patchText)) {
    return null;
  }

  if ((preRelease.length > 0 && !isValidIdentifierSet(preRelease)) ||
      (buildMetadata.length > 0 && !isValidIdentifierSet(buildMetadata))) {
    return null;
  }

  return { major, minor, patch, preRelease, buildMetadata };
}

/** Renders the version in canonical wire form: major.minor.patch[-pre-release][+build]. */
export function formatVersion(version: SemVer): string {
  let result = `${version.major}.${version.minor}.${version.patch}`;
  if (version.preRelease.length > 0) {
    result += `-${version.preRelease}`;
  }
  if (version.buildMetadata.length > 0) {
    result += `+${version.buildMetadata}`;
  }
  return result;
}

function comparePreReleaseIdentifier(left: string, right: string): number {
  const leftNumber = /^[0-9]+$/.test(left) ? Number.parseInt(left, 10) : Number.NaN;
  const rightNumber = /^[0-9]+$/.test(right) ? Number.parseInt(right, 10) : Number.NaN;
  if (!Number.isNaN(leftNumber) && !Number.isNaN(rightNumber)) {
    return leftNumber - rightNumber;
  }
  if (!Number.isNaN(leftNumber)) {
    return -1; // Numeric identifiers always sort below alphanumeric identifiers.
  }
  if (!Number.isNaN(rightNumber)) {
    return 1;
  }
  return left < right ? -1 : left > right ? 1 : 0;
}

function comparePreRelease(left: string, right: string): number {
  const leftEmpty = left.length === 0;
  const rightEmpty = right.length === 0;
  if (leftEmpty && rightEmpty) {
    return 0;
  }
  if (leftEmpty) {
    return 1; // A release version has higher precedence than any pre-release of the same core.
  }
  if (rightEmpty) {
    return -1;
  }

  const leftIds = left.split('.');
  const rightIds = right.split('.');
  const count = Math.min(leftIds.length, rightIds.length);
  for (let i = 0; i < count; i += 1) {
    const comparison = comparePreReleaseIdentifier(leftIds[i] ?? '', rightIds[i] ?? '');
    if (comparison !== 0) {
      return comparison;
    }
  }
  return leftIds.length - rightIds.length;
}

/**
 * Compares two versions using SemVer precedence rules (build metadata ignored).
 * Returns a negative value when left precedes right, zero when equal precedence, positive otherwise.
 */
export function compareVersions(left: SemVer, right: SemVer): number {
  if (left.major !== right.major) {
    return left.major - right.major;
  }
  if (left.minor !== right.minor) {
    return left.minor - right.minor;
  }
  if (left.patch !== right.patch) {
    return left.patch - right.patch;
  }
  return comparePreRelease(left.preRelease, right.preRelease);
}

/** True when the version is the all-zero not-provided sentinel. */
export function isEmptyVersion(version: SemVer): boolean {
  return version.major === 0 && version.minor === 0 && version.patch === 0 &&
    version.preRelease.length === 0 && version.buildMetadata.length === 0;
}
