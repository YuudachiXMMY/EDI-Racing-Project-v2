# Code Review: Host Token Model (PRD Phase 1)

**Reviewed**: 2026-07-28
**Mode**: Local (commit `342ea92` on `feat/host-token-model`, not yet pushed)
**Decision**: ✅ APPROVE with comments

## Summary
Clean, well-scoped Phase 1. The HMAC token verify is correct (length-guarded timing-safe compare, never throws, deterministic tests) and faithfully mirrors existing codebase conventions. No CRITICAL or HIGH issues. The one thing that MUST be handled before enforcement is turned on in production is the default `INTERNAL_SECRET` (MEDIUM-1) — with the flag off (default), there is no live risk.

## Findings

### CRITICAL
None.

### HIGH
None.

### MEDIUM

**MEDIUM-1 — Default `INTERNAL_SECRET` makes the token gate bypassable if enforcement is enabled without overriding it.**
`web-app/src/hostToken.js:19` and `Server/server.js` both fall back to `process.env.INTERNAL_SECRET || 'edi-internal-default'`. That default is committed in the repo, so once `REQUIRE_HOST_TOKEN=true` becomes the auth boundary (Phase 2+), anyone who knows the public default can mint their own valid token and fully bypass the gate. This mirrors the pre-existing posture of the `/api/sessions/archive` endpoint, so it is not a regression — but it becomes security-critical the moment the flag is flipped.
*Fix (before Phase 2 / any prod enablement)*: refuse to run with `REQUIRE_HOST_TOKEN=true` while `INTERNAL_SECRET` is unset/default (fail-fast on boot, or emit a loud startup warning), and document that a strong random `INTERNAL_SECRET` is mandatory when enforcement is on. Not blocking for this commit because the flag defaults off.

### LOW

**LOW-2 — Token is not bound to a user/session/room and is replayable within its TTL.**
A minted token authorizes "become a host" generically and can create multiple rooms during its 5-min window. Acceptable for a single-professor classroom trust model, but Phase 2 should consider binding the token to the `sessionId` or making it single-use.

**LOW-3 — Verified `surveyId` is discarded in `create_room`.**
`Server/server.js` computes `result.surveyId` but does not use it. Intended — Phase 3 (auto-inject) consumes it. No action now; noted so it isn't mistaken for dead code.

**LOW-4 — Server-side `verifyHostToken` has no direct unit test.**
Only the web-app (ESM) copy is unit-tested; the CJS copy is covered by the cross-process integration smoke only (`Server/` has no test harness). Drift risk is mitigated by the byte-for-byte spec comment and the integration test, but a tiny Node-native test for the server copy would harden it.

## Validation Results

| Check | Result |
|---|---|
| Static (`node --check` × 3) | ✅ Pass |
| Unit tests (`vitest run`) | ✅ Pass — 25/25 (8 new, 17 existing, no regressions) |
| Build (Unity recompile / `docker compose config`) | ✅ Pass — 0 compile errors, compose renders |
| Integration (cross-process mint→verify + backward-compat) | ✅ Pass |

## Category Assessment

| Category | Verdict |
|---|---|
| Correctness | ✅ Timing-safe compare with length guard; never throws; boundary (`exp-1ms`) tested |
| Type/format safety | ✅ Byte-for-byte wire spec shared across ESM/CJS; version field gates payload |
| Pattern compliance | ✅ Matches env-default, `sendJSON({type:'error'})`, `{success,data}` envelope, `[Auth]` log prefix |
| Security | ⚠️ MEDIUM-1 (default secret when flag on); gate correctly transitively protects all host broadcasts |
| Performance | ✅ Local HMAC verify, no round-trip; stateless |
| Completeness | ✅ Tests + integration; `.env.example` deferred (permission-blocked, tracked in report) |
| Maintainability | ✅ Documented duplication with lockstep warning; no magic numbers |

## Files Reviewed
- `web-app/src/hostToken.js` — Added
- `web-app/src/routes/game-status.js` — Modified
- `web-app/__tests__/host-token.test.js` — Added
- `Server/server.js` — Modified
- `Assets/Scripts/Network/NetworkManager.cs` — Modified
- `Assets/Scripts/Network/NetworkMessages.cs` — Modified
- `Deploy/docker-compose.yml` — Modified
- `.claude/PRPs/{prds,plans,reports}/…` — Docs (not code-reviewed)

## Required Before Enforcement (Phase 2)
1. Fail-fast / warn when `REQUIRE_HOST_TOKEN=true` and `INTERNAL_SECRET` is default (MEDIUM-1).
2. Add `REQUIRE_HOST_TOKEN` / `HOST_TOKEN_TTL_MS` to both `.env.example` files.
3. Consider session-binding / single-use for the token (LOW-2).
