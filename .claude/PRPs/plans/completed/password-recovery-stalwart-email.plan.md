# Plan: Password Recovery via Stalwart Email (web-app)

## Summary
Add a self-service "forgot password" flow to the web-app professor accounts. A user requests a
reset by email; the Express backend mints a single-use, DB-stored token and emails a reset link via
the self-hosted Stalwart SMTP server (credentials configured in env). Clicking the link opens a
client page where the user sets a new password. SMTP credentials are configured through `.env` in
local dev and injected on the deploy server through `apps/ediracing/.env.extra`
(`/Users/jadyn/Development/IthacaServer`).

## User Story
As a **professor with a web-app account**,
I want **to reset my password by email when I forget it**,
so that **I can regain access without contacting an administrator**.

## Problem → Solution
Today `web-app/src/routes/auth.js` only supports register/login/logout — a forgotten password
locks the user out permanently (there is no admin UI, and sessions are in-memory). →
Add `POST /api/auth/forgot-password` + `POST /api/auth/reset-password`, a DB-backed single-use
token table, a Stalwart-backed mailer, and client screens to complete the round-trip.

## Metadata
- **Complexity**: Medium–Large
- **Source PRD**: N/A (free-form request)
- **PRD Phase**: N/A
- **Estimated Files**: ~14 (7 web-app source, 3 client, 2 tests, 2 deploy config)

---

## UX Design

### Before
```
┌─────────────────────────────────────┐
│  Professor Login                     │
│  [ Email                          ]  │
│  [ Password (min 6 chars)         ]  │
│  [        Log In        ]            │
│  Don't have an account? Register     │
└─────────────────────────────────────┘
   (forgot password → dead end)
```

### After
```
┌─────────────────────────────────────┐        ┌─────────────────────────────────────┐
│  Professor Login                     │        │  Reset Your Password                 │
│  [ Email                          ]  │        │  (opened from emailed link,          │
│  [ Password (min 6 chars)         ]  │        │   token in URL)                      │
│  [        Log In        ]            │        │  [ New Password (min 6 chars)     ]  │
│  Forgot password?  ·  Register       │        │  [ Confirm Password               ]  │
└─────────────────────────────────────┘        │  [     Set New Password     ]        │
        │  click "Forgot password?"             └─────────────────────────────────────┘
        ▼                                                   ▲
┌─────────────────────────────────────┐   email link       │
│  Forgot Password                     │   https://<host>/  │
│  [ Email                          ]  │   #/reset-password │
│  [   Send Reset Link    ]            │   ?token=<raw>     │
│  "If that email is registered, a     │───────────────────┘
│   reset link has been sent."         │
└─────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Login screen | Register toggle only | Adds "Forgot password?" link | Reuses existing `.toggle-link` styling |
| Forgot-password | — | New view collects email, always shows generic success | No user enumeration |
| Email inbox | — | Stalwart-sent message with reset link | `MAIL_FROM` = `noreply@<domain>` |
| Reset screen | — | New route `/reset-password?token=…` sets new password, redirects to login | Token is single-use |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/auth.js` | 1-66 | Exact route/response contract to extend (`{ success, error }` / `{ success, data }`) |
| P0 | `web-app/src/hostToken.js` | 1-134 | Crypto/token conventions: `randomBytes`, HMAC, `timingSafeEqual`, boot-guard style, `process.env.*` TTL parsing |
| P0 | `web-app/src/db.js` | 25-107 | Where schema is loaded + `applyMigrations` idempotent-migration pattern (new table must be added in BOTH schema.sql and here) |
| P0 | `web-app/src/schema.sql` | 1-8 | `users` table shape (`id`, `password_hash`) that resets reference |
| P0 | `web-app/src/middleware/auth.js` | 1-28 | In-memory session store — reset should invalidate a user's sessions |
| P1 | `web-app/src/config.js` | 1-30 | Centralized `process.env` config module — add SMTP/mail config here |
| P1 | `web-app/client/src/api.js` | 19-68 | `request()` helper + login/register client fns to mirror |
| P1 | `web-app/client/src/pages/LoginPage.jsx` | 1-77 | Form/error/loading pattern + `.toggle-link` markup to mirror |
| P1 | `web-app/client/src/App.jsx` | 1-30 | HashRouter route table — add `/forgot-password` + `/reset-password` |
| P1 | `web-app/__tests__/host-token.test.js` | all | Deterministic `now`-injected token test pattern to mirror |
| P1 | `web-app/__tests__/test-helpers.js` | 1-37 | `createTestDb()` (in-memory) + `createTestUser()` fixtures |
| P2 | `web-app/src/index.js` | 14-47 | Boot-guard pattern (warn/fatal on misconfig) — mirror for missing SMTP config warning |
| P2 | `IthacaServer/apps/ediracing/docker-compose.yml` | 67-107 | web-app service env block + networks (needs `proxy` added) |
| P2 | `IthacaServer/apps/ediracing/.env.extra.example` | 1-14 | Deploy secret injection format |
| P2 | `IthacaServer/infrastructure/mail/README.md` | 64-88 | Stalwart "App Integration" — SMTP host/port + proxy-network requirement |
| P2 | `IthacaServer/scripts/provision.sh` | 131-166 | How `.env.extra` is appended to server `.env` |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Nodemailer SMTP transport | https://nodemailer.com/smtp/ | `createTransport({ host, port, secure, auth:{user,pass} })`; `secure:true` for 465, `false`+STARTTLS for 587; `transport.sendMail({from,to,subject,text,html})` returns a Promise |
| Stalwart app integration | `IthacaServer/infrastructure/mail/README.md` | Internal apps reach `stalwart:25` on the `proxy` Docker network; auth with a service account (user + password) for authenticated submission on 587 |
| Node `--env-file` | package.json `start`/`dev` scripts | `.env` is loaded via `node --env-file=.env` — new SMTP vars belong in `.env` locally; no dotenv package |

```
KEY_INSIGHT: Nodemailer is the standard, well-audited SMTP client for Node. No built-in SMTP exists.
APPLIES_TO: Task 4 (mailer.js), package.json dependency.
GOTCHA: Adding a runtime dependency needs approval per .claude/docs/technical-preferences.md
        ("Allowed Libraries: None configured yet"). Flagged in Risks — get sign-off before install.

KEY_INSIGHT: The web-app container is on the `internal` network only; Stalwart is on `proxy`.
APPLIES_TO: Task 12 (docker-compose.yml).
GOTCHA: Without adding `proxy` to the web-app service, `stalwart:25/587` is unreachable and every
        send fails with ENOTFOUND. Must add the network to the service (the top-level
        `networks.proxy: { external: true }` already exists).

KEY_INSIGHT: The SPA is served at the site ROOT ("/"), uses HashRouter, and reads token from URL.
APPLIES_TO: reset link construction. Base is https://<DOMAIN>/ (prod) or http://localhost:3001 (dev).
GOTCHA: Link must be `${APP_BASE_URL}/#/reset-password?token=<raw>` — the `/#/` is required by HashRouter.
```

---

## Patterns to Mirror

### NAMING_CONVENTION — routes & responses
```js
// SOURCE: web-app/src/routes/auth.js:9-35
router.post('/register', (req, res) => {
  const { email, password, displayName } = req.body;
  if (!email || !password) {
    return res.status(400).json({ success: false, error: 'Email and password required' });
  }
  if (password.length < 6) {
    return res.status(400).json({ success: false, error: 'Password must be at least 6 characters' });
  }
  const db = getDb();
  const hash = bcrypt.hashSync(password, 10);
  // ...
  res.status(201).json({ success: true, data: { /* ... */ } });
});
```

### CRYPTO_TOKEN — random token + hashing (never store raw)
```js
// SOURCE: web-app/src/hostToken.js:1, 82-85  (randomBytes + hex/b64url conventions)
import { randomBytes, createHash, timingSafeEqual } from 'crypto';
// generate: randomBytes(32).toString('hex')  → the raw token emailed to the user
// store:    createHash('sha256').update(raw).digest('hex')  → the only thing persisted
```

### ENV_CONFIG — centralized process.env with defaults
```js
// SOURCE: web-app/src/config.js:8, hostToken.js:25
export const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
const TTL_MS = parseInt(process.env.HOST_TOKEN_TTL_MS || '300000', 10); // 5 min
```

### BOOT_GUARD — warn on missing config at startup (fail-open, loud)
```js
// SOURCE: web-app/src/index.js:42-47
if (!archiveSecretUsable(process.env.INTERNAL_SECRET)) {
  console.warn('[Auth] WARNING: /api/sessions/archive is DISABLED — INTERNAL_SECRET is unset ...');
}
```

### IDEMPOTENT_MIGRATION — new table in schema.sql + db.js
```js
// SOURCE: web-app/src/db.js:70-85
db.exec(`CREATE TABLE IF NOT EXISTS game_sessions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER REFERENCES users(id),
  ...
)`);
```

### SESSION_STORE — in-memory Map keyed by token
```js
// SOURCE: web-app/src/middleware/auth.js:4-14
const sessions = new Map(); // token -> { userId, email }
export function destroySession(token) { sessions.delete(token); }
// New helper mirrors this: iterate entries, delete those whose value.userId === userId
```

### CLIENT_API — fetch wrapper + typed helpers
```js
// SOURCE: web-app/client/src/api.js:47-63
export async function login(email, password) {
  const result = await request('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) });
  if (result.success) setToken(result.data.token);
  return result;
}
```

### CLIENT_FORM — page with loading/error state
```jsx
// SOURCE: web-app/client/src/pages/LoginPage.jsx:14-30
async function handleSubmit(e) {
  e.preventDefault(); setError(''); setLoading(true);
  const result = await register(email, password, displayName);
  setLoading(false);
  if (result.success) navigate('/dashboard'); else setError(result.error || 'Something went wrong');
}
```

### TEST_STRUCTURE — deterministic token tests with injected `now`
```js
// SOURCE: web-app/__tests__/host-token.test.js (pattern), test-helpers.js:15-37
import { describe, it, expect } from 'vitest';
import { createTestDb, createTestUser } from './test-helpers.js';
// const db = createTestDb(); const { userId } = createTestUser(db);
// pass now = 1_000_000 into create/consume for expiry assertions — no Date.now()
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/schema.sql` | UPDATE | Add `password_resets` table |
| `web-app/src/db.js` | UPDATE | Add idempotent `CREATE TABLE IF NOT EXISTS password_resets` in `applyMigrations` |
| `web-app/src/config.js` | UPDATE | Add `mailConfig` (SMTP_* ) + `APP_BASE_URL` + `mailConfigured()` helper |
| `web-app/src/lib/passwordReset.js` | CREATE | Pure DB logic: create/consume/hash reset tokens (unit-testable) |
| `web-app/src/lib/mailer.js` | CREATE | Nodemailer transport + `sendPasswordResetEmail()` (isolated I/O) |
| `web-app/src/middleware/auth.js` | UPDATE | Add `destroySessionsForUser(userId)` |
| `web-app/src/routes/auth.js` | UPDATE | Add `POST /forgot-password` + `POST /reset-password` |
| `web-app/src/index.js` | UPDATE | Startup warning when mail is unconfigured (mirror boot-guard) |
| `web-app/package.json` | UPDATE | Add `nodemailer` dependency (pending approval) |
| `web-app/.env.example` | UPDATE | Document `SMTP_HOST/PORT/SECURE/USER/PASS`, `MAIL_FROM`, `APP_BASE_URL` |
| `web-app/client/src/api.js` | UPDATE | Add `requestPasswordReset()` + `resetPassword()` |
| `web-app/client/src/pages/ForgotPasswordPage.jsx` | CREATE | Email entry → generic success |
| `web-app/client/src/pages/ResetPasswordPage.jsx` | CREATE | Reads `?token=`, sets new password |
| `web-app/client/src/pages/LoginPage.jsx` | UPDATE | Add "Forgot password?" link |
| `web-app/client/src/App.jsx` | UPDATE | Add `/forgot-password` + `/reset-password` routes |
| `web-app/__tests__/passwordReset.test.js` | CREATE | Token create/consume/expiry/single-use tests |
| `web-app/__tests__/mailer.test.js` | CREATE | Message construction (no network — inject transport) |
| `IthacaServer/apps/ediracing/docker-compose.yml` | UPDATE | Add SMTP env to `web-app` + join `proxy` network |
| `IthacaServer/apps/ediracing/.env.extra.example` | UPDATE | Document `SMTP_PASS` + mail vars for deploy |

## NOT Building
- Password-reset for **student** accounts (students have no accounts — surveys use share codes).
- Admin-triggered password reset / user management UI.
- Email verification on registration (separate feature).
- Rate limiting beyond a simple per-email cooldown (see Risks — recommended follow-up).
- Migrating the in-memory session store to persistent/JWT sessions.
- Changing Stalwart server config, DNS, or DKIM (infra is already provisioned).
- Localization of the email body (English only, matching current UI).

---

## Step-by-Step Tasks

### Task 1: Add `password_resets` table
- **ACTION**: Add table to `web-app/src/schema.sql`; add idempotent `CREATE TABLE IF NOT EXISTS` in `web-app/src/db.js` `applyMigrations`.
- **IMPLEMENT**:
  ```sql
  -- Password reset tokens (single-use, short-lived). Only the SHA-256 hash is stored.
  CREATE TABLE IF NOT EXISTS password_resets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users(id),
    token_hash TEXT NOT NULL UNIQUE,
    expires_at INTEGER NOT NULL,          -- epoch ms
    used_at INTEGER DEFAULT NULL,         -- epoch ms; set on consume
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
  );
  CREATE INDEX IF NOT EXISTS idx_password_resets_token ON password_resets(token_hash);
  ```
- **MIRROR**: IDEMPOTENT_MIGRATION (db.js:70-85).
- **IMPORTS**: none.
- **GOTCHA**: Must appear in BOTH schema.sql (fresh DB) AND db.js applyMigrations (existing prod DB) — the test harness `createTestDb()` runs schema.sql + applyMigrations, so a table missing from one path breaks tests or prod silently.
- **VALIDATE**: `cd web-app && npm test` — existing `db.test.js` / `migrations.test.js` still pass.

### Task 2: SMTP + mail config in `config.js`
- **ACTION**: Extend `web-app/src/config.js` with mail settings and a readiness helper.
- **IMPLEMENT**:
  ```js
  // Stalwart SMTP settings (see infrastructure/mail/README.md). Password is env-configured.
  export const mailConfig = {
    host: process.env.SMTP_HOST || 'stalwart',
    port: parseInt(process.env.SMTP_PORT || '587', 10),
    secure: (process.env.SMTP_SECURE || 'false').toLowerCase() === 'true', // true=465, false=587/STARTTLS
    user: process.env.SMTP_USER || '',
    pass: process.env.SMTP_PASS || '',
    from: process.env.MAIL_FROM || 'noreply@localhost',
  };
  // Public base URL for building reset links. SPA is served at site root with HashRouter.
  export const APP_BASE_URL = (process.env.APP_BASE_URL || 'http://localhost:3001').replace(/\/$/, '');
  // Mail is usable only when host + from are set AND (no auth) or (both user+pass present).
  export function mailConfigured() {
    return Boolean(mailConfig.host && mailConfig.from && (!mailConfig.user || mailConfig.pass));
  }
  ```
- **MIRROR**: ENV_CONFIG (config.js:8, hostToken.js:25).
- **IMPORTS**: none new.
- **GOTCHA**: `APP_BASE_URL` must have no trailing slash so `${APP_BASE_URL}/#/reset-password` is well-formed.
- **VALIDATE**: `node -e "import('./web-app/src/config.js').then(m=>console.log(m.mailConfigured()))"` prints a boolean.

### Task 3: `src/lib/passwordReset.js` — pure DB token logic
- **ACTION**: Create the reset-token service (testable, no network).
- **IMPLEMENT**:
  ```js
  import { randomBytes, createHash } from 'crypto';

  const RESET_TTL_MS = parseInt(process.env.RESET_TOKEN_TTL_MS || '3600000', 10); // 1 hour

  export function hashToken(rawToken) {
    return createHash('sha256').update(rawToken).digest('hex');
  }

  /** Create a single-use reset record for a user. Returns the RAW token (emailed, never stored). */
  export function createResetToken(db, userId, now = Date.now()) {
    const raw = randomBytes(32).toString('hex');
    // Invalidate any outstanding tokens for this user (only the newest link should work).
    db.prepare('UPDATE password_resets SET used_at = ? WHERE user_id = ? AND used_at IS NULL')
      .run(now, userId);
    db.prepare('INSERT INTO password_resets (user_id, token_hash, expires_at) VALUES (?, ?, ?)')
      .run(userId, hashToken(raw), now + RESET_TTL_MS);
    return raw;
  }

  /** Verify + atomically consume a token. Returns { valid, userId? , error? }. Never throws. */
  export function consumeResetToken(db, rawToken, now = Date.now()) {
    if (typeof rawToken !== 'string' || rawToken.length === 0) {
      return { valid: false, error: 'missing token' };
    }
    const row = db.prepare('SELECT * FROM password_resets WHERE token_hash = ?').get(hashToken(rawToken));
    if (!row) return { valid: false, error: 'invalid token' };
    if (row.used_at !== null) return { valid: false, error: 'token already used' };
    if (row.expires_at <= now) return { valid: false, error: 'token expired' };
    db.prepare('UPDATE password_resets SET used_at = ? WHERE id = ?').run(now, row.id);
    return { valid: true, userId: row.user_id };
  }
  ```
- **MIRROR**: CRYPTO_TOKEN (hostToken.js), IDEMPOTENT_MIGRATION.
- **IMPORTS**: `crypto`.
- **GOTCHA**: Never compare/store the raw token; only its SHA-256 hash. Consume is single-use — set `used_at` before returning valid.
- **VALIDATE**: covered by Task 13 tests.

### Task 4: `src/lib/mailer.js` — Nodemailer transport
- **ACTION**: Isolate all email I/O behind one module with an injectable transport (for tests).
- **IMPLEMENT**:
  ```js
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
  ```
- **MIRROR**: module-per-concern (lib/*.js).
- **IMPORTS**: `nodemailer`, `mailConfig`.
- **GOTCHA**: `auth` must be `undefined` (not `{user:'',pass:''}`) when no user — Stalwart on the internal relay allow-list accepts unauthenticated relay, and passing empty creds forces a failing AUTH. The `transport` param lets tests inject a fake `{ sendMail }`.
- **VALIDATE**: Task 13 mailer test asserts `buildResetEmail` fields; no live send in tests.

### Task 5: `destroySessionsForUser` in middleware/auth.js
- **ACTION**: Add helper to drop all in-memory sessions for a user (called after reset).
- **IMPLEMENT**:
  ```js
  export function destroySessionsForUser(userId) {
    for (const [token, session] of sessions) {
      if (session.userId === userId) sessions.delete(token);
    }
  }
  ```
- **MIRROR**: SESSION_STORE (auth.js:4-14).
- **IMPORTS**: none.
- **GOTCHA**: Deleting from a Map while iterating its entries is safe in JS. `userId` type must match what `createSession` stored (number from `lastInsertRowid`/`user.id`).
- **VALIDATE**: add an assertion in `auth.test.js` (or passwordReset test) that a session for the user is gone after the call.

### Task 6: Reset routes in routes/auth.js
- **ACTION**: Add `POST /forgot-password` and `POST /reset-password`.
- **IMPLEMENT**:
  ```js
  import { createResetToken, consumeResetToken } from '../lib/passwordReset.js';
  import { sendPasswordResetEmail } from '../lib/mailer.js';
  import { destroySessionsForUser } from '../middleware/auth.js';
  import { APP_BASE_URL, mailConfigured } from '../config.js';

  // POST /api/auth/forgot-password  — always generic success (no user enumeration)
  router.post('/forgot-password', async (req, res) => {
    const { email } = req.body;
    const generic = { success: true, data: { message: 'If that email is registered, a reset link has been sent.' } };
    if (!email) return res.status(400).json({ success: false, error: 'Email required' });

    const db = getDb();
    const user = db.prepare('SELECT id, email FROM users WHERE email = ?').get(email);
    if (user && mailConfigured()) {
      const raw = createResetToken(db, user.id);
      const resetUrl = `${APP_BASE_URL}/#/reset-password?token=${raw}`;
      try {
        await sendPasswordResetEmail(user.email, resetUrl);
      } catch (err) {
        console.error('[Auth] Failed to send reset email:', err.message); // swallow — stay generic
      }
    } else if (user && !mailConfigured()) {
      console.warn('[Auth] forgot-password requested but mail is not configured — no email sent.');
    }
    res.json(generic);
  });

  // POST /api/auth/reset-password  — { token, password }
  router.post('/reset-password', (req, res) => {
    const { token, password } = req.body;
    if (!token || !password) {
      return res.status(400).json({ success: false, error: 'Token and password required' });
    }
    if (password.length < 6) {
      return res.status(400).json({ success: false, error: 'Password must be at least 6 characters' });
    }
    const db = getDb();
    const result = consumeResetToken(db, token);
    if (!result.valid) {
      return res.status(400).json({ success: false, error: 'Invalid or expired reset link' });
    }
    const hash = bcrypt.hashSync(password, 10);
    db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(hash, result.userId);
    destroySessionsForUser(result.userId); // force re-login everywhere
    res.json({ success: true, data: { message: 'Password updated. You can now log in.' } });
  });
  ```
- **MIRROR**: NAMING_CONVENTION (auth.js register/login).
- **IMPORTS**: as shown; keep existing `bcrypt`, `getDb`.
- **GOTCHA**: Validation error text and password rule (`>= 6`) must match register exactly (auth.js:15-17). `forgot-password` must return the SAME response and status whether or not the email exists — never 404. The route handler is `async` (mailer returns a Promise); Express 4 does not catch async throws, so all awaits are wrapped in try/catch.
- **VALIDATE**: manual curl (see Validation Commands) + integration assertion.

### Task 7: Startup warning in index.js
- **ACTION**: Warn (not fatal) at boot when mail is unconfigured, mirroring the archive-secret guard.
- **IMPLEMENT**:
  ```js
  import { mailConfigured } from './config.js';
  if (!mailConfigured()) {
    console.warn('[Mail] WARNING: SMTP is not fully configured — password-reset emails are DISABLED. ' +
      'Set SMTP_HOST/MAIL_FROM (+ SMTP_USER/SMTP_PASS if authenticating).');
  }
  ```
- **MIRROR**: BOOT_GUARD (index.js:42-47).
- **IMPORTS**: `mailConfigured`.
- **GOTCHA**: Warn only — the app must still boot and serve login for existing users when mail is down.
- **VALIDATE**: start server without SMTP vars → warning prints, server still listens.

### Task 8: Env documentation (web-app)
- **ACTION**: Append mail vars to `web-app/.env.example`.
- **IMPLEMENT**:
  ```dotenv
  # --- Password-reset email (Stalwart SMTP) ---
  SMTP_HOST=stalwart          # internal Docker DNS on the `proxy` network; localhost for local test
  SMTP_PORT=587               # 587 STARTTLS (with auth) or 465 implicit TLS or 25 internal relay
  SMTP_SECURE=false           # true only for port 465
  SMTP_USER=noreply@your-domain.tld   # service account; leave blank to use unauthenticated relay
  SMTP_PASS=                  # <-- configure the password here (empty = no auth)
  MAIL_FROM=noreply@your-domain.tld
  APP_BASE_URL=http://localhost:3001  # public origin; https://<DOMAIN> in production
  RESET_TOKEN_TTL_MS=3600000  # 1 hour
  ```
- **MIRROR**: existing `.env.example` layout.
- **IMPORTS**: n/a.
- **GOTCHA**: `.env` is real config (gitignored); only `.env.example` is committed. Never commit real `SMTP_PASS`.
- **VALIDATE**: `node --env-file=.env src/index.js` loads without error.

### Task 9: Client API helpers
- **ACTION**: Add `requestPasswordReset` + `resetPassword` to `web-app/client/src/api.js`.
- **IMPLEMENT**:
  ```js
  export async function requestPasswordReset(email) {
    return request('/auth/forgot-password', { method: 'POST', body: JSON.stringify({ email }) });
  }
  export async function resetPassword(token, password) {
    return request('/auth/reset-password', { method: 'POST', body: JSON.stringify({ token, password }) });
  }
  ```
- **MIRROR**: CLIENT_API (api.js:47-63).
- **IMPORTS**: reuses local `request`.
- **GOTCHA**: These are unauthenticated — but `request()` is fine (no token is simply omitted). Both return 200/400, so the 401 auto-redirect path in `request()` is not triggered.
- **VALIDATE**: build client (`cd client && npm run build`) with no import errors.

### Task 10: Client pages + routes
- **ACTION**: Create `ForgotPasswordPage.jsx` and `ResetPasswordPage.jsx`; wire routes in `App.jsx`; add link in `LoginPage.jsx`.
- **IMPLEMENT**:
  - `ForgotPasswordPage`: email input → `requestPasswordReset`; on success show the generic message (do not reveal existence). Mirror LoginPage structure/`.login-card`.
  - `ResetPasswordPage`: read token via react-router `useSearchParams` (works with HashRouter's post-hash query); new + confirm password (min 6, must match) → `resetPassword`; on success `navigate('/login')`.
  - `App.jsx`: add `<Route path="/forgot-password" element={<ForgotPasswordPage />} />` and `<Route path="/reset-password" element={<ResetPasswordPage />} />` (both public).
  - `LoginPage.jsx`: add near the toggle link: `<a href="#/forgot-password">Forgot password?</a>`
- **MIRROR**: CLIENT_FORM (LoginPage.jsx:14-30).
- **IMPORTS**: `useState`, `useNavigate`/`useSearchParams` from `react-router-dom`, the two new api fns.
- **GOTCHA**: With HashRouter, `useSearchParams` reads the query AFTER the `#` correctly; if reading manually, parse `window.location.hash`, not `window.location.search`. Confirm-password mismatch must be caught client-side before calling the API.
- **VALIDATE**: `cd client && npm run build` succeeds; manual browser walkthrough.

### Task 11: Add nodemailer dependency
- **ACTION**: `cd web-app && npm install nodemailer` (pending approval — see Risks).
- **IMPLEMENT**: adds `"nodemailer": "^6.9.x"` to `dependencies`; commit updated `package-lock.json`.
- **MIRROR**: existing deps in package.json.
- **IMPORTS**: n/a.
- **GOTCHA**: Dockerfile stage 2 runs `npm ci --omit=dev` — nodemailer must be a normal dependency, NOT devDependency, or the prod image won't have it. Pure-JS package → no native build concerns (unlike better-sqlite3).
- **VALIDATE**: `npm ci --omit=dev` in a clean checkout includes `node_modules/nodemailer`.

### Task 12: Deploy config (IthacaServer)
- **ACTION**: Add SMTP env + `proxy` network to the `web-app` service; document deploy secrets.
- **IMPLEMENT**:
  - `apps/ediracing/docker-compose.yml`, `web-app.environment`, add:
    ```yaml
    SMTP_HOST: "${SMTP_HOST:-stalwart}"
    SMTP_PORT: "${SMTP_PORT:-587}"
    SMTP_SECURE: "${SMTP_SECURE:-false}"
    SMTP_USER: "${SMTP_USER:-}"
    SMTP_PASS: "${SMTP_PASS:-}"
    MAIL_FROM: "${MAIL_FROM:-noreply@${DOMAIN}}"
    APP_BASE_URL: "${APP_BASE_URL:-https://${DOMAIN}}"
    ```
  - `web-app.networks`: add `proxy` (currently only `internal`):
    ```yaml
    networks:
      - internal
      - proxy
    ```
    (The top-level `networks.proxy: { external: true }` already exists.)
  - `apps/ediracing/.env.extra.example`: append
    ```dotenv
    # --- Password-reset email (Stalwart) ---
    SMTP_HOST=stalwart
    SMTP_PORT=587
    SMTP_SECURE=false
    SMTP_USER=noreply@<MAIL_DOMAIN>
    SMTP_PASS=change-me            # <-- the mailbox password, injected on the deploy server
    MAIL_FROM=noreply@<MAIL_DOMAIN>
    ```
- **MIRROR**: docker-compose env-interpolation pattern (compose:30, 80-81); provision.sh `.env.extra` append (provision.sh:131-139).
- **IMPORTS**: n/a.
- **GOTCHA**: The web-app container CANNOT reach Stalwart without the `proxy` network — this is the #1 failure mode. `MAIL_FROM`/`APP_BASE_URL` default off `${DOMAIN}` (already in the generated `.env`). Real `SMTP_PASS` goes ONLY in the server-side `.env.extra` (gitignored), never in `.env.extra.example`. If using unauthenticated internal relay instead, leave `SMTP_USER`/`SMTP_PASS` blank and add the Docker subnet to Stalwart's relay allow-list (mail README).
- **VALIDATE**: `docker compose -f apps/ediracing/docker-compose.yml config` resolves without unset-var errors; after deploy, `docker exec ediracing-web-app node -e "require('dns').lookup('stalwart',console.log)"` resolves.

### Task 13: Tests
- **ACTION**: Create `passwordReset.test.js` and `mailer.test.js`.
- **IMPLEMENT**:
  - `passwordReset.test.js` (mirror host-token.test.js + test-helpers):
    - creates raw token; `consumeResetToken` returns `{valid:true, userId}` once.
    - second consume of same token → `{valid:false, error:'token already used'}`.
    - expired token (`now` past `expires_at`) → `{valid:false, error:'token expired'}`.
    - unknown/garbage token → `{valid:false, error:'invalid token'}`.
    - creating a new token marks the previous one used (only newest works).
    - `destroySessionsForUser` removes the user's session (import createSession + requireAuth-style check).
  - `mailer.test.js`:
    - `buildResetEmail('a@b.com','https://x/#/reset-password?token=t')` → correct `to`, `subject`, and `text/html` contain the URL and `from === mailConfig.from`.
    - `sendPasswordResetEmail` with an injected fake transport (`{ sendMail: async m => m }`) resolves with the built message (no network).
- **MIRROR**: TEST_STRUCTURE.
- **IMPORTS**: `vitest`, `createTestDb`, `createTestUser`.
- **GOTCHA**: Determinism — always pass explicit `now`; never call `Date.now()` in assertions. No real SMTP in tests (inject transport).
- **VALIDATE**: `cd web-app && npm test` — all suites green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| consume valid token | fresh token, now < exp | `{valid:true, userId}` | No |
| consume twice | same token, 2nd call | `{valid:false, error:'token already used'}` | Yes |
| consume expired | now > expires_at | `{valid:false, error:'token expired'}` | Yes |
| consume unknown | random string | `{valid:false, error:'invalid token'}` | Yes |
| consume empty | `''` / undefined | `{valid:false, error:'missing token'}` | Yes |
| newest-only | create twice, consume 1st | old token invalid, new valid | Yes |
| session purge | createSession then reset | user's session removed | Yes |
| buildResetEmail | email + url | to/subject/from set, url in body | No |
| sendPasswordResetEmail | fake transport | resolves with message | No |

### Edge Cases Checklist
- [x] Empty input (missing token/email/password) → 400 with correct message
- [x] Maximum size input (long token) → treated as invalid, no crash
- [x] Invalid types (non-string token) → `missing token`
- [x] Concurrent access (two tokens for same user) → only newest valid
- [x] Network failure (SMTP down) → forgot-password still returns generic success; error logged
- [x] Permission denied (mail unconfigured) → boot warns; forgot-password logs + returns generic success
- [x] User does not exist → generic success, no email, no error leak

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npx oxlint    # client lint (config: client/.oxlintrc.json)
```
EXPECT: Zero errors on changed files

### Unit Tests
```bash
cd web-app && npm test
```
EXPECT: All suites pass, including new passwordReset & mailer tests

### Build (client + prod deps)
```bash
cd web-app/client && npm run build          # SPA build, no import errors
cd web-app && npm ci --omit=dev             # prod deps include nodemailer
```
EXPECT: Client dist built; nodemailer present in node_modules

### Deploy compose validation
```bash
cd /Users/jadyn/Development/IthacaServer && docker compose -f apps/ediracing/docker-compose.yml config
```
EXPECT: Renders with proxy network on web-app; no unset-var errors

### Manual API smoke (local, SMTP pointed at a catcher or real Stalwart)
```bash
# 1) request reset (always 200, generic)
curl -s -XPOST localhost:3001/api/auth/forgot-password -H 'Content-Type: application/json' \
  -d '{"email":"prof@university.edu"}'
# 2) copy token from the email link, then:
curl -s -XPOST localhost:3001/api/auth/reset-password -H 'Content-Type: application/json' \
  -d '{"token":"<RAW_TOKEN>","password":"newpass1"}'
# 3) login with the new password
curl -s -XPOST localhost:3001/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"prof@university.edu","password":"newpass1"}'
```
EXPECT: (1) generic success; (2) `{success:true}`; (3) returns a token

### Manual Validation
- [ ] Login page shows "Forgot password?"; click → forgot-password view
- [ ] Submit registered email → generic success message (no "user exists" leak)
- [ ] Submit unregistered email → same generic message
- [ ] Reset email arrives from `MAIL_FROM` via Stalwart; link opens reset page
- [ ] New password + confirm mismatch → client blocks before API call
- [ ] Valid reset → redirected to login; old password rejected; new password works
- [ ] Reusing the same link → "Invalid or expired reset link"
- [ ] With SMTP unconfigured → boot warning; forgot-password still returns 200

---

## Acceptance Criteria
- [ ] `POST /api/auth/forgot-password` and `/reset-password` implemented per contract
- [ ] Reset tokens are single-use, hashed at rest, 1-hour TTL
- [ ] Email sent via Stalwart SMTP with password configurable through env
- [ ] Deploy server reads `SMTP_PASS` from `apps/ediracing/.env.extra`; web-app joined to `proxy` network
- [ ] Client forgot/reset screens work end-to-end
- [ ] No user enumeration (generic responses)
- [ ] Password reset invalidates the user's active sessions
- [ ] All validation commands pass; new unit tests green

## Completion Checklist
- [ ] Code follows discovered patterns (routes, crypto, config, migrations, client API/forms)
- [ ] Error handling matches `{ success, error }` contract + status codes
- [ ] Logging uses `[Tag] message` console style (e.g. `[Auth]`, `[Mail]`)
- [ ] Tests follow deterministic `now`-injected, in-memory-DB pattern
- [ ] No hardcoded SMTP creds/URLs — all via env
- [ ] `.env.example` + `.env.extra.example` documented
- [ ] nodemailer added as a runtime dependency (approved)
- [ ] No unnecessary scope additions

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| New `nodemailer` dep needs approval (technical-preferences "Allowed Libraries: none") | High | Low | Get explicit sign-off before Task 11; pure-JS, widely used, no native build |
| web-app not on `proxy` network → cannot reach Stalwart | Medium | High | Task 12 adds `proxy`; validate with in-container DNS lookup |
| Stalwart rejects unauthenticated relay / auth misconfig | Medium | Medium | Use service-account auth (SMTP_USER/PASS) on 587; fall back to relay allow-list per mail README |
| Reset-link abuse / email bombing (no rate limit) | Medium | Medium | Per-email cooldown follow-up; tokens are single-use + short TTL; generic responses limit leak |
| Async route throwing uncaught in Express 4 | Low | Medium | All awaits wrapped in try/catch (Task 6) |
| Real `SMTP_PASS` committed by mistake | Low | High | Only `.env.example`/`.env.extra.example` are committed; real secrets in gitignored `.env`/`.env.extra` |

## Decisions (confirmed by user)
- **nodemailer**: APPROVED as a runtime dependency. Add to `dependencies` and record it under
  `.claude/docs/technical-preferences.md` → "Allowed Libraries / Addons".
- **SMTP auth**: Service account + password on port **587 (STARTTLS)**. `SMTP_USER`/`SMTP_PASS`
  are REQUIRED. Prerequisite: create a `noreply@<MAIL_DOMAIN>` mailbox in the Stalwart admin UI
  before deploy. The unauthenticated internal-relay path is NOT used.

## Notes
- **Auth path**: authenticated submission (SMTP_USER + SMTP_PASS on port 587), per the decision above.
- **Session persistence caveat**: Sessions are in-memory, so a reset only clears sessions on the
  running process. Acceptable for the current single-instance deploy; noted for any future scale-out.
- **Reset link base**: SPA is served at site root with HashRouter, so links are
  `${APP_BASE_URL}/#/reset-password?token=…` (prod `https://<DOMAIN>`, dev `http://localhost:3001`).
- **Follow-up (out of scope)**: per-email rate limit, registration email verification, admin reset.
