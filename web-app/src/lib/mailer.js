import nodemailer from 'nodemailer';
import { mailConfig } from '../config.js';

let _transport = null;
function getTransport() {
  if (_transport) return _transport;
  _transport = nodemailer.createTransport({
    host: mailConfig.host,
    port: mailConfig.port,
    secure: mailConfig.secure,
    auth: mailConfig.user ? { user: mailConfig.user, pass: mailConfig.pass } : undefined,
  });
  return _transport;
}

/** Build the reset email message. Pure — exported for tests (no network). */
export function buildResetEmail(toEmail, resetUrl) {
  return {
    from: mailConfig.from,
    to: toEmail,
    subject: 'Reset your EDI Survey password',
    text: `Someone requested a password reset for this account.\n\n` +
          `Reset your password: ${resetUrl}\n\n` +
          `This link expires in 1 hour. If you did not request this, ignore this email.`,
    html: `<p>Someone requested a password reset for this account.</p>` +
          `<p><a href="${resetUrl}">Reset your password</a></p>` +
          `<p>This link expires in 1 hour. If you did not request this, you can ignore this email.</p>`,
  };
}

/** Send the reset email. Returns a Promise; caller logs+swallows failures (no enumeration). */
export async function sendPasswordResetEmail(toEmail, resetUrl, transport = getTransport()) {
  return transport.sendMail(buildResetEmail(toEmail, resetUrl));
}
