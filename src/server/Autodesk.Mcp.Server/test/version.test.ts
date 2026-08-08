import { describe, expect, it } from 'vitest';
import { compareVersions, formatVersion, isEmptyVersion, tryParseVersion } from '../src/protocol/version.js';

describe('semver helpers (mirror VersionInformation)', () => {
  it('parses the canonical wire form', () => {
    expect(formatVersion(tryParseVersion('1.2.3')!)).toBe('1.2.3');
  });

  it('accepts a two-part core version as 1.2.0', () => {
    const version = tryParseVersion('1.2')!;
    expect(version.minor).toBe(2);
    expect(version.patch).toBe(0);
    expect(formatVersion(version)).toBe('1.2.0');
  });

  it('parses pre-release and build metadata', () => {
    const version = tryParseVersion('1.0.0-beta.1+sha.abc')!;
    expect(version.preRelease).toBe('beta.1');
    expect(version.buildMetadata).toBe('sha.abc');
    expect(formatVersion(version)).toBe('1.0.0-beta.1+sha.abc');
  });

  it('rejects malformed versions', () => {
    expect(tryParseVersion('')).toBeNull();
    expect(tryParseVersion('1.2.3.4')).toBeNull();
    expect(tryParseVersion('1.0.0-')).toBeNull();
    expect(tryParseVersion('x.y.z')).toBeNull();
    expect(tryParseVersion('1..0')).toBeNull();
  });

  it('treats 0.0.0 as the not-provided sentinel', () => {
    expect(isEmptyVersion(tryParseVersion('0.0.0')!)).toBe(true);
    expect(isEmptyVersion(tryParseVersion('1.0.0')!)).toBe(false);
  });

  it('compares by SemVer precedence, ignoring build metadata', () => {
    expect(compareVersions(tryParseVersion('1.0.0')!, tryParseVersion('2.0.0')!)).toBeLessThan(0);
    expect(compareVersions(tryParseVersion('1.0.0')!, tryParseVersion('1.1.0')!)).toBeLessThan(0);
    expect(compareVersions(tryParseVersion('1.0.0')!, tryParseVersion('1.0.1')!)).toBeLessThan(0);
    expect(compareVersions(tryParseVersion('1.0.0+build1')!, tryParseVersion('1.0.0+build2')!)).toBe(0);
  });

  it('orders release above pre-release of the same core', () => {
    expect(compareVersions(tryParseVersion('1.0.0')!, tryParseVersion('1.0.0-beta')!)).toBeGreaterThan(0);
    expect(compareVersions(tryParseVersion('1.0.0-beta.2')!, tryParseVersion('1.0.0-beta.11')!)).toBeLessThan(0);
    expect(compareVersions(tryParseVersion('1.0.0-alpha')!, tryParseVersion('1.0.0-beta')!)).toBeLessThan(0);
  });
});
