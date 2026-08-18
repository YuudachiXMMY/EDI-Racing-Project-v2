import { describe, it, expect } from 'vitest';
import { createSession, destroySession, requireAuth } from '../src/middleware/auth.js';

describe('Auth Middleware', () => {
  it('createSession returns a hex token', () => {
    const token = createSession(1, 'test@test.com');
    expect(typeof token).toBe('string');
    expect(token.length).toBe(64); // 32 bytes = 64 hex chars
  });

  it('requireAuth rejects missing Authorization header', () => {
    const req = { headers: {} };
    const res = { status: (code) => ({ json: (body) => { res._code = code; res._body = body; } }) };
    const next = () => { res._next = true; };

    requireAuth(req, res, next);

    expect(res._code).toBe(401);
    expect(res._next).toBeUndefined();
  });

  it('requireAuth rejects invalid token', () => {
    const req = { headers: { authorization: 'Bearer invalidtoken123' } };
    const res = { status: (code) => ({ json: (body) => { res._code = code; res._body = body; } }) };
    const next = () => { res._next = true; };

    requireAuth(req, res, next);

    expect(res._code).toBe(401);
  });

  it('requireAuth accepts valid session token', () => {
    const token = createSession(42, 'prof@university.edu');
    const req = { headers: { authorization: `Bearer ${token}` } };
    const res = { status: () => ({ json: () => {} }) };
    let nextCalled = false;
    const next = () => { nextCalled = true; };

    requireAuth(req, res, next);

    expect(nextCalled).toBe(true);
    expect(req.user).toEqual({ userId: 42, email: 'prof@university.edu' });
  });

  it('destroySession invalidates the token', () => {
    const token = createSession(1, 'a@b.com');
    destroySession(token);

    const req = { headers: { authorization: `Bearer ${token}` } };
    const res = { status: (code) => ({ json: (body) => { res._code = code; } }) };
    const next = () => {};

    requireAuth(req, res, next);

    expect(res._code).toBe(401);
  });

  it('requireAuth rejects non-Bearer scheme', () => {
    const req = { headers: { authorization: 'Basic abc123' } };
    const res = { status: (code) => ({ json: (body) => { res._code = code; } }) };
    const next = () => { res._next = true; };

    requireAuth(req, res, next);

    expect(res._code).toBe(401);
    expect(res._next).toBeUndefined();
  });
});
