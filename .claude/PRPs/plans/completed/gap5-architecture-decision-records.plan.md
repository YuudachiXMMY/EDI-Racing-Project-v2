# Plan: Architecture Decision Records (ADRs)

## Summary
从三个 PRD 的 Decisions Log 中提取关键架构决策，使用项目的 ADR 模板创建正式的 ADR 文档，存放在 `docs/architecture/`。当前该目录仅有一个空的 `tr-registry.yaml`，违反了 coding standards 中"每个系统必须有对应的 ADR"的要求。

## User Story
As a project maintainer, I want architectural decisions formally documented as ADRs, so that future contributors understand why technical choices were made and can evaluate their ongoing relevance.

## Problem → Solution
**Current state**: `docs/architecture/` 目录仅有 `tr-registry.yaml`（空的）。20 个架构决策分散在 3 个 PRD 的 Decisions Log 表格中，没有按 ADR 格式独立存档。Coding standards 明确要求 "Every system must have a corresponding architecture decision record in `docs/architecture/`"。
**Desired state**: 关键架构决策以 ADR 格式存档在 `docs/architecture/`，提供可追溯的决策历史。

## Metadata
- **Complexity**: Medium
- **Source PRD**: PRP Gap Analysis — GAP 5
- **PRD Phase**: N/A
- **Estimated Files**: 7-10 (ADR files) + 1 index

---

## UX Design

N/A — internal documentation change, no user-facing UX transformation.

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `.claude/docs/templates/architecture-decision-record.md` | all | ADR template to follow |
| P0 (critical) | `.claude/PRPs/prds/edi-racing-v2.prd.md` | 282-294 | Decisions Log — 8 decisions |
| P0 (critical) | `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md` | 258-271 | Decisions Log — 9 decisions |
| P0 (critical) | `.claude/PRPs/prds/edi-survey-web-app.prd.md` | 253-264 | Decisions Log — 7 decisions |
| P1 (important) | `.claude/docs/coding-standards.md` | all | ADR requirement source |
| P1 (important) | `docs/architecture/tr-registry.yaml` | all | Existing TR registry (currently empty) |
| P2 (reference) | `.claude/docs/technical-preferences.md` | all | Current tech stack details |

## External Documentation

No external documentation needed — all decisions are already documented in PRDs.

---

## Patterns to Mirror

### ADR_TEMPLATE
```markdown
// SOURCE: .claude/docs/templates/architecture-decision-record.md:1-175
// Full ADR template with sections:
// Status, Date, Last Verified, Decision Makers, Summary,
// Engine Compatibility, ADR Dependencies, Context, Decision,
// Alternatives Considered, Consequences, Risks,
// Performance Implications, Migration Plan, Validation Criteria,
// GDD Requirements Addressed, Related
```

### ADR_NAMING
```
// Convention derived from template:
// Filename: ADR-NNNN-kebab-case-title.md
// e.g., ADR-0001-render-pipeline-urp.md
// Sequential numbering starting from 0001
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `docs/architecture/ADR-0001-render-pipeline-urp.md` | CREATE | URP rendering pipeline decision |
| `docs/architecture/ADR-0002-webgl-build-target.md` | CREATE | WebGL as primary build target |
| `docs/architecture/ADR-0003-websocket-multi-client-sync.md` | CREATE | WebSocket + deterministic sim for multi-client |
| `docs/architecture/ADR-0004-docker-deployment.md` | CREATE | Docker-based deployment strategy |
| `docs/architecture/ADR-0005-dynamic-attribute-model.md` | CREATE | Dynamic attributes (Dictionary) for flexible data |
| `docs/architecture/ADR-0006-interpreted-rule-engine.md` | CREATE | Runtime-interpreted rules engine |
| `docs/architecture/ADR-0007-web-app-stack-react-express-sqlite.md` | CREATE | React + Express + SQLite web app stack |
| `docs/architecture/ADR-0008-navmesh-car-navigation.md` | CREATE | NavMesh Agent for car navigation |
| `docs/architecture/README.md` | CREATE | ADR index with links to all ADRs |

## NOT Building

- Full ADR template sections for every decision — use a **simplified ADR format** appropriate for a project that has already shipped these decisions
- ADRs for minor decisions (UI framework UGUI, question types, attribute typing) — only document cross-cutting architectural decisions
- TR-registry entries — that's a separate concern for `/architecture-review`
- Control Manifest — separate concern
- ADRs for future decisions — only document what's already decided and implemented

---

## Step-by-Step Tasks

### Task 1: Consolidate Decisions from PRDs

- **ACTION**: Extract and deduplicate the 20+ decisions across 3 PRDs into ~8 unique architectural decisions
- **IMPLEMENT**: Cross-reference the three PRD decision logs. Many decisions overlap (e.g., "WebSocket" appears in both edi-racing-v2 and flexible-survey). Group into consolidated ADRs:
  1. **Render Pipeline**: URP (edi-racing-v2)
  2. **Build Target**: WebGL (edi-racing-v2)
  3. **Multi-Client Sync**: WebSocket + deterministic sim (edi-racing-v2, flexible-survey)
  4. **Deployment**: Docker Compose (edi-racing-v2, edi-survey-web-app)
  5. **Dynamic Data Model**: Dictionary-based attributes (flexible-survey)
  6. **Rule Engine**: Interpreted rules at runtime (flexible-survey)
  7. **Web App Stack**: React + Express + SQLite + SurveyJS (edi-survey-web-app)
  8. **Car Navigation**: NavMesh Agent (edi-racing-v2)
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Some decisions span multiple PRDs with slightly different wording. Use the most complete rationale.
- **VALIDATE**: Every significant architectural decision from the PRDs is captured.

### Task 2: Create ADR-0001 — Render Pipeline (URP)

- **ACTION**: Write ADR for choosing URP over Standard RP and HDRP
- **IMPLEMENT**: Create `docs/architecture/ADR-0001-render-pipeline-urp.md` using simplified ADR format:
  - **Status**: Accepted
  - **Context**: WebGL target requires balanced rendering; HDRP too heavy, Standard RP deprecated
  - **Decision**: URP 17.3.0
  - **Alternatives**: Standard RP (deprecated), HDRP (too heavy for WebGL)
  - **Consequences**: Good WebGL performance; limited advanced rendering features
  - **Engine Compatibility**: Unity 6.3 LTS, Knowledge Risk LOW
- **MIRROR**: ADR_TEMPLATE (simplified — omit Performance Implications table, Migration Plan, GDD Requirements for already-accepted decisions)
- **IMPORTS**: N/A
- **GOTCHA**: Keep concise — this decision is already implemented and stable
- **VALIDATE**: File exists, Status is "Accepted", key sections present

### Task 3: Create ADR-0002 — WebGL Build Target

- **ACTION**: Write ADR for WebGL as primary build target
- **IMPLEMENT**: Create `docs/architecture/ADR-0002-webgl-build-target.md`
  - **Context**: Browser access is core requirement for classroom use; no install needed
  - **Decision**: WebGL primary, Editor for development
  - **Alternatives**: Desktop standalone (requires install), Mobile (not applicable)
  - **Consequences**: Memory ceiling 2048MB; some Unity features unavailable; browser compatibility considerations
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 4: Create ADR-0003 — WebSocket Multi-Client Sync

- **ACTION**: Write ADR for WebSocket-based synchronization
- **IMPLEMENT**: Create `docs/architecture/ADR-0003-websocket-multi-client-sync.md`
  - **Context**: WebGL lacks UDP/direct networking; need lightweight sync for events, not full state
  - **Decision**: WebSocket + deterministic simulation; sync events only
  - **Alternatives**: Unity Netcode (requires relay, complex), State streaming (bandwidth heavy)
  - **Consequences**: Lightweight; works in WebGL; requires deterministic game logic
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 5: Create ADR-0004 — Docker Deployment

- **ACTION**: Write ADR for Docker-based deployment
- **IMPLEMENT**: Create `docs/architecture/ADR-0004-docker-deployment.md`
  - **Context**: Professor self-hosts; needs simple, reproducible deployment
  - **Decision**: Docker Compose (nginx + Node.js WS server)
  - **Alternatives**: GitHub Pages (static only), Cloud hosting (cost, complexity)
  - **Consequences**: Single container; works offline; professor manages Docker
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 6: Create ADR-0005 — Dynamic Attribute Model

- **ACTION**: Write ADR for Dictionary-based dynamic attributes
- **IMPLEMENT**: Create `docs/architecture/ADR-0005-dynamic-attribute-model.md`
  - **Context**: Survey questions must map to game attributes without code changes
  - **Decision**: Dictionary<string, string> with typed accessors
  - **Alternatives**: Fixed fields (inflexible), ECS components (overengineered)
  - **Consequences**: Maximum flexibility; type safety at access time, not storage time
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 7: Create ADR-0006 — Interpreted Rule Engine

- **ACTION**: Write ADR for runtime-interpreted rules
- **IMPLEMENT**: Create `docs/architecture/ADR-0006-interpreted-rule-engine.md`
  - **Context**: Professor defines event rules through UI; rules evaluated per-event not per-frame
  - **Decision**: Interpreted rules evaluated at runtime with configurable operators
  - **Alternatives**: Code generation (over-complex), Expression trees (unnecessary)
  - **Consequences**: Simple; extensible operators; not performance-critical
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 8: Create ADR-0007 — Web App Stack

- **ACTION**: Write ADR for React + Express + SQLite + SurveyJS stack
- **IMPLEMENT**: Create `docs/architecture/ADR-0007-web-app-stack-react-express-sqlite.md`
  - **Context**: Need web-based survey tool that integrates with Unity game; existing Node.js WS server
  - **Decision**: React frontend, Express backend, SQLite DB, SurveyJS for survey rendering
  - **Alternatives**: Vue/Angular (less ecosystem), Python FastAPI (different stack), MongoDB/MySQL (heavier)
  - **Consequences**: Unified Node.js stack; SQLite is zero-config; SurveyJS handles complex survey UIs
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 9: Create ADR-0008 — NavMesh Car Navigation

- **ACTION**: Write ADR for NavMesh Agent-based car movement
- **IMPLEMENT**: Create `docs/architecture/ADR-0008-navmesh-car-navigation.md`
  - **Context**: Autonomous racing cars need reliable track navigation
  - **Decision**: NavMesh Agent (AI Navigation 2.0.13)
  - **Alternatives**: Physics-based (unreliable), Spline-following (rigid)
  - **Consequences**: Proven in v1; reliable; requires baked NavMesh on track
- **MIRROR**: ADR_TEMPLATE (simplified)
- **VALIDATE**: File exists with correct content

### Task 10: Create ADR Index (README.md)

- **ACTION**: Create an index file listing all ADRs with status and one-line summary
- **IMPLEMENT**: Create `docs/architecture/README.md` with:
  ```markdown
  # Architecture Decision Records

  | ADR | Title | Status | Date |
  |-----|-------|--------|------|
  | [ADR-0001](ADR-0001-render-pipeline-urp.md) | Render Pipeline: URP | Accepted | 2025-XX-XX |
  | ... |
  ```
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Date should reflect when the original decision was made (from PRD creation dates), not when the ADR was written
- **VALIDATE**: README lists all 8 ADRs with correct links

---

## Testing Strategy

No automated tests — documentation-only task.

### Manual Validation
- [ ] All 8 ADR files exist in `docs/architecture/`
- [ ] Each ADR follows the template structure (at minimum: Status, Summary, Context, Decision, Alternatives, Consequences)
- [ ] README.md links to all ADRs
- [ ] All ADR statuses are "Accepted" (these are already-implemented decisions)
- [ ] No contradictions between ADRs and actual implementation

---

## Validation Commands

### File Count
```bash
ls docs/architecture/ADR-*.md 2>/dev/null | wc -l
```
EXPECT: 8

### README Links Valid
```bash
grep -oP '\(ADR-\d{4}[^)]+\.md\)' docs/architecture/README.md | tr -d '()' | while read f; do
  test -f "docs/architecture/$f" && echo "OK: $f" || echo "MISSING: $f"
done
```
EXPECT: All OK

### All ADRs Have Required Sections
```bash
for f in docs/architecture/ADR-*.md; do
  echo "=== $(basename $f) ==="
  grep -c "^## Status\|^## Context\|^## Decision\|^## Consequences" "$f"
done
```
EXPECT: Each file shows 4 (has all 4 required sections)

---

## Acceptance Criteria
- [ ] 8 ADR files created in `docs/architecture/`
- [ ] Each ADR has Status, Summary, Context, Decision, Alternatives Considered, Consequences
- [ ] Each ADR status is "Accepted"
- [ ] README.md index created with links to all ADRs
- [ ] Key decisions from all 3 PRDs are covered
- [ ] Coding standards requirement "every system must have ADR" is at least partially fulfilled

## Completion Checklist
- [ ] ADRs follow the project template (`.claude/docs/templates/architecture-decision-record.md`)
- [ ] Simplified format used (omit Performance Implications, Migration Plan for accepted decisions)
- [ ] Engine Compatibility section included for engine-specific decisions
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| ADRs become stale as project evolves | MEDIUM | LOW | Each ADR has "Last Verified" field; review periodically |
| Missing decisions not in PRD logs | LOW | LOW | ADRs can be added incrementally; start with what's documented |
| Template is too heavy for retroactive docs | MEDIUM | MEDIUM | Use simplified format — skip inapplicable sections |

## Notes
- These are **retroactive ADRs** — the decisions are already made and implemented. The ADRs document history, not propose changes.
- The ADR template has many sections (Engine Compatibility, ADR Dependencies, Performance Implications, Migration Plan, GDD Requirements). For retroactive ADRs of accepted decisions, use a **simplified format**: include Status, Date, Summary, Context, Decision, Alternatives Considered, Consequences, and Engine Compatibility (for engine-specific decisions). Omit Performance Implications tables, Migration Plan, and GDD Requirements unless directly relevant.
- Total effort: ~2-3 hours (mostly writing; no code changes).
- ADR numbering starts at 0001 to leave room for future decisions.
- Date for each ADR should use the approximate date when the decision was originally made (e.g., from PRD creation or project milestone dates), not the date the ADR document was written.
