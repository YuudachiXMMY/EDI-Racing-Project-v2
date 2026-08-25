import { describe, it, expect } from 'vitest';
import { buildResetEmail, sendPasswordResetEmail } from '../src/lib/mailer.js';
import { mailConfig } from '../src/config.js';

const RESET_URL = 'https://x/#/reset-password?token=deadbeef';

describe('buildResetEmail', () => {
  it('sets to / subject / from and embeds the reset URL in both bodies', () => {
    const msg = buildResetEmail('a@b.com', RESET_URL);
    expect(msg.to).toBe('a@b.com');
    expect(msg.from).toBe(mailConfig.from);
    expect(msg.subject).toBe('Reset your EDI Survey password');
    expect(msg.text).toContain(RESET_URL);
    expect(msg.html).toContain(RESET_URL);
    expect(msg.html).toContain(`href="${RESET_URL}"`);
  });
});

describe('sendPasswordResetEmail', () => {
  it('sends the built message through an injected transport (no network)', async () => {
    // Fake transport returns the message it was handed so we can assert on it.
    const fakeTransport = { sendMail: async (m) => m };
    const result = await sendPasswordResetEmail('a@b.com', RESET_URL, fakeTransport);
    expect(result).toEqual(buildResetEmail('a@b.com', RESET_URL));
  });

  it('rejects when the transport fails, so the caller can log + stay generic', async () => {
    const failing = { sendMail: async () => { throw new Error('SMTP down'); } };
    await expect(sendPasswordResetEmail('a@b.com', RESET_URL, failing)).rejects.toThrow('SMTP down');
  });
});
