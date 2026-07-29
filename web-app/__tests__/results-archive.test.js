import { describe, it, expect } from 'vitest';
import { archiveSecretUsable } from '../src/routes/results.js';
import { DEFAULT_INTERNAL_SECRET } from '../src/hostToken.js';

// The archive endpoint (POST /api/sessions/archive) can write session records against any
// professor account, so it must fail closed unless a strong secret is set. archiveSecretUsable
// is the pure decision that gates it — mirrored in style on hostToken's checkSecretConfig.
describe('archiveSecretUsable', () => {
  it('rejects the public default secret', () => {
    expect(archiveSecretUsable(DEFAULT_INTERNAL_SECRET)).toBe(false);
  });

  it('rejects an unset (undefined) secret', () => {
    expect(archiveSecretUsable(undefined)).toBe(false);
  });

  it('rejects an empty string secret', () => {
    expect(archiveSecretUsable('')).toBe(false);
  });

  it('rejects a non-string secret without throwing', () => {
    expect(archiveSecretUsable(42)).toBe(false);
    expect(archiveSecretUsable(null)).toBe(false);
  });

  it('accepts a strong non-default secret', () => {
    expect(archiveSecretUsable('phase7-adversarial-qa-secret-0123456789abcdef')).toBe(true);
  });
});
