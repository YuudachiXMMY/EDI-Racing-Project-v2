# Flexible Survey Designer & Configurable Attribute Mapping

## Problem Statement

EDI Racing v2 reproduces v1's hardcoded data model: `CarData` has exactly 3 fixed fields (TeamName, ColorIndex, Functions), `CsvParser` expects exactly that column order, and `EventMatcher` uses a switch statement over 7 enum values with type-specific targeting fields. This means professors outside the original ENGG*1100 course cannot use the tool without modifying C# source code — they cannot define their own survey questions, cannot add custom attributes to cars, and cannot create new event rules that match on arbitrary data. The tool is locked to one course's survey design.

## Evidence

- v1 was used across 18 releases (Oct 2023 - Nov 2024) in University of Guelph ENGG*1100 but could not expand to other courses due to hardcoded survey field mapping (columns G-P)
- v2 PRD explicitly deferred "Flexible question designer" to v2.1 (Won't scope) and noted "Configurable attribute-to-speed mapping rules" as Should
- Current `CarData` struct has only 3 fields: `TeamName`, `ColorIndex` (0-4), `Functions` (string array) — no room for arbitrary survey responses
- Current `EventMatcher.IsAffected()` is a switch statement with hardcoded matching logic for each of 7 enum values
- Current `CsvParser` expects exactly `teamName,colorIndex,functionList` with no header row
- Assumption - needs validation: professors from other universities/courses will adopt if customization barrier is removed

## Proposed Solution

Replace the hardcoded 3-field data model with a dynamic attribute system where professors define arbitrary survey questions (via JSON config files and/or runtime UI), CSV files use header rows for dynamic column mapping, and a rule engine replaces the hardcoded EventMatcher with configurable "attribute → comparison → effect" rules. Students can answer surveys directly in their browser via the existing WebSocket infrastructure. The entire configuration (questions, mapping rules, event rules) is persisted as JSON and shareable across sessions.

## Key Hypothesis

We believe a flexible survey designer and configurable attribute mapping system will enable professors from any course to create custom EDI racing demonstrations without modifying code.
We'll know we're right when a professor with no prior exposure to the tool can set up a complete custom survey + race session (different questions from ENGG*1100) in under 15 minutes using only the in-game UI.

## What We're NOT Building

- Real-time student car control — students spectate only, cars are data-driven
- Survey branching logic — questions are linear, no conditional jumps (e.g., "if A then skip to Q5")
- Statistical analysis of survey results — this is a visualization tool, not a research instrument
- LMS integration or SSO — standalone anonymous participation
- Question types beyond text, multiple-choice, and numeric — no file upload, matrix, or ranking questions
- Multi-language survey UI — English only for v2.1

## Success Metrics

| Metric | Target | How Measured |
|--------|--------|--------------|
| Custom survey setup time | < 15 minutes for new professor | Timed user testing with unfamiliar user |
| CSV column flexibility | Any number of columns with headers | Feature verification |
| Custom event rules | Professor can create rules on any attribute | Feature verification |
| Student survey completion | < 2 minutes per student | Timed user testing |
| v1 feature parity | All 7 v1 event types reproducible via custom rules | Feature verification |
| Config portability | Survey config exportable/importable as JSON | Feature verification |

## Open Questions

- [ ] Should the survey builder support question validation rules (e.g., "number between 1-10")? Likely yes for numeric attributes used in mapping.
- [ ] Should survey responses be anonymized or linked to team names? Current v1 model uses team names.
- [ ] How should the UI handle large numbers of custom attributes (10+) in the event rule builder?
- [ ] Should custom rules support compound conditions (e.g., "color IS blue AND function CONTAINS glasses")? Start with single conditions, add compound later.

---

## Users & Context

**Primary User 1: Professor (Survey Designer)**
- **Who**: University instructor teaching any EDI-related course, not necessarily tech-savvy
- **Current behavior**: Stuck with v1's hardcoded ENGG*1100 survey fields; cannot customize without editing C# code
- **Trigger**: Beginning of semester — needs to set up a survey specific to their course content (e.g., accessibility survey, gender equity survey, cultural diversity survey)
- **Success state**: Creates custom survey questions in-game, defines how responses map to car attributes, configures event rules that test EDI concepts relevant to their specific course, saves configuration for reuse next semester

**Primary User 2: Student (Survey Respondent)**
- **Who**: University student participating in a class EDI activity
- **Current behavior**: No direct survey participation in v2 — data comes from CSV import only
- **Trigger**: Professor announces activity, shares room code/URL
- **Success state**: Opens browser, joins room, answers survey questions on their device, sees their team's car appear in the race

**Job to Be Done**

Professor: When preparing an EDI demonstration for my specific course, I want to define custom survey questions and control how responses affect the race, so that I can create a demonstration tailored to my course's EDI themes without technical help.

Student: When participating in a class EDI activity, I want to quickly answer survey questions on my phone/laptop and see my responses influence the race, so that I feel personally invested in the demonstration.

**Non-Users**
- Developers/technical users who prefer editing code directly — the tool should be self-service
- Researchers needing statistically rigorous survey design — this is a demonstration tool
- K-12 teachers — UI and content designed for university level

---

## Solution Detail

### Core Capabilities (MoSCoW)

| Priority | Capability | Rationale |
|----------|------------|-----------|
| Must | Dynamic CarData with arbitrary key-value attributes | Foundation — everything else depends on flexible data model |
| Must | Dynamic CSV parser with header row support | Import pathway for flexible data |
| Must | Configurable event rule engine (attribute → comparison → effect) | Core value — professors create custom rules without code |
| Must | JSON-based survey/mapping configuration file | Persistence, sharing, and portability of configurations |
| Must | Runtime survey builder UI for professors | Self-service — no config file editing required |
| Must | Runtime attribute mapping UI for professors | Self-service — visual rule creation |
| Must | In-game student survey UI via WebSocket | Students participate from their browsers |
| Should | Survey template library (pre-built configs for common EDI themes) | Lowers barrier — professors start from templates, not blank canvas |
| Should | Question validation rules (numeric range, required fields) | Data quality for mapping rules |
| Should | Survey response preview (professor sees responses as they arrive) | Real-time feedback during data collection phase |
| Could | Survey config import/export (share configs between professors) | Community/reuse value |
| Could | Compound rule conditions (AND/OR multiple attribute checks) | Advanced rule flexibility |
| Won't | Survey branching/conditional logic | Complexity not justified for demonstration tool |
| Won't | Real-time student car control | Cars are data-driven, not player-driven |
| Won't | Multi-language survey UI | English only for v2.1 |

### MVP Scope

All Must-have capabilities in a single release:
1. Dynamic data model replacing hardcoded CarData
2. Dynamic CSV parser with headers
3. Rule engine replacing EventMatcher
4. JSON config files for survey + rules
5. Runtime survey builder UI
6. Runtime rule/mapping builder UI
7. Student browser survey via WebSocket

### User Flow

```
Professor Flow:
1. Launch game -> Setup Screen
2. "New Survey" -> Survey Builder UI opens
   a. Add questions: "What is your primary language?" (text), "Rate accessibility" (1-10), etc.
   b. Define attribute mappings: question responses -> car attributes (color, speed modifier, tag)
   c. Configure events: "If language != English, apply -5 speed for 8s" (custom rule)
   d. Save configuration as JSON
3. "Host Room" -> Room code displayed
4. Students join via browser -> Answer survey on their device
5. Professor sees responses arrive in real-time
6. "Start Race" -> Cars spawn with survey-driven attributes
7. During race -> Trigger custom events via Events Panel
8. Race ends -> Discuss outcomes, export results

Student Flow:
1. Open browser -> Enter room code/URL
2. Survey questions appear on screen
3. Answer each question (text input, multiple choice, number slider)
4. See confirmation: "Your team car has been created!"
5. Watch race in spectator mode

Returning Professor Flow:
1. Launch game -> Setup Screen
2. "Load Configuration" -> Select saved JSON config
3. All questions, mappings, and rules restored
4. "Host Room" -> Proceed as above
```

---

## Technical Approach

**Feasibility**: MEDIUM-HIGH

The core challenge is replacing a deeply embedded hardcoded data model (CarData struct touches 8+ files) with a dynamic attribute system while preserving the working race mechanics. The WebSocket infrastructure for student participation already exists. Unity's JsonUtility does not support Dictionary serialization, requiring either a custom serialization wrapper or switching to a third-party JSON library (Newtonsoft.Json is available via Unity Package Manager).

**Architecture Notes**

```
NEW COMPONENTS:
+------------------------------------------------------------------+
|  SurveyConfig (ScriptableObject / JSON)                          |
|  - List<SurveyQuestion> Questions                                |
|  - List<AttributeMapping> Mappings (question -> car attribute)   |
|  - List<EventRule> CustomRules (attribute + comparison + effect)  |
+------------------------------------------------------------------+
         ↓                           ↓                    ↓
  SurveyBuilderUI          MappingBuilderUI        RuleBuilderUI
  (Professor runtime)      (Professor runtime)     (Professor runtime)
         ↓                           ↓                    ↓
  StudentSurveyUI ←──WebSocket──→ SurveyCollector (server-side)
         ↓
  SurveyResponseParser → List<CarData> (now with dynamic attributes)
         ↓
  [existing pipeline: CarSpawner → CarController → EventManager]

MODIFIED COMPONENTS:
- CarData: string TeamName + Dictionary<string, string> Attributes
- CarIdentity: stores dynamic attributes
- CsvParser: reads header row, maps columns to attributes
- EventMatcher: replaced by RuleEngine evaluating EventRule list
- RaceEventConfig: replaced by EventRule (attribute, comparison, value, effect)
- SessionData: stores SurveyConfig + responses
- NetworkMessages: carries survey questions and responses
```

**Key Design Decisions**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Attribute storage | `Dictionary<string, string>` with typed accessors | Flexible enough for any attribute type; string-based for JSON serialization |
| JSON serialization | Custom wrapper over JsonUtility (or Newtonsoft.Json) | JsonUtility doesn't support Dictionary; need reliable serialization |
| Rule evaluation | Interpreted at runtime (not compiled) | Rules change frequently; performance is not critical (evaluated once per event trigger) |
| Survey transport | WebSocket messages (existing infrastructure) | Already have room/session management; no new server needed |
| Config format | JSON files in persistentDataPath | Human-readable, portable, version-controllable |

**Technical Risks**

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Data model refactor breaks existing race mechanics | MEDIUM | Incremental refactoring with tests at each step; keep TeamName as first-class field |
| JsonUtility Dictionary limitation | HIGH (known) | Use serializable list-of-pairs wrapper (`[{key,value}]`) or add Newtonsoft.Json |
| Survey UI complexity in Unity UGUI | MEDIUM | Keep UI minimal — list of questions with type selectors, not a full form builder |
| Network message size with survey data | LOW | Survey responses are small text; existing WebSocket handles game state already |
| Rule engine performance with many rules | LOW | Rules evaluated only on event trigger, not per-frame; 50 cars × 20 rules = trivial |
| Breaking change disrupts existing sessions | LOW (accepted) | User accepts breaking change; provide migration guide for v1 CSV files |

---

## Implementation Phases

| # | Phase | Description | Status | Parallel | Depends | PRP Plan |
|---|-------|-------------|--------|----------|---------|----------|
| 1 | Dynamic Data Model | Refactor CarData to dynamic attributes, update CsvParser, CarIdentity, SessionData, NetworkMessages | complete | - | - | [dynamic-data-model.plan.md](../plans/completed/dynamic-data-model.plan.md) |
| 2 | Custom Rule Engine | Replace EventMatcher with configurable rule engine, new EventRule data model, update EventManager | pending | - | 1 | - |
| 3 | Survey Config System | JSON schema for SurveyConfig (questions + mappings + rules), file I/O, template library | pending | with 2 | 1 | - |
| 4 | Professor Builder UI | Runtime UI for survey builder, attribute mapping editor, and event rule editor | pending | - | 2, 3 | - |
| 5 | Student Survey System | In-game survey UI for students, WebSocket survey collection, response-to-CarData pipeline | pending | with 4 | 1, 3 | - |
| 6 | Integration & Testing | Wire all systems together, end-to-end testing, migration guide, documentation | pending | - | 4, 5 | - |

### Phase Details

**Phase 1: Dynamic Data Model**
- **Goal**: Replace the hardcoded 3-field CarData with a flexible attribute system that supports arbitrary key-value pairs
- **Scope**: Refactor `CarData` struct to use attribute dictionary; update `CsvParser` to read header rows and map columns dynamically; update `CarIdentity` to store/expose dynamic attributes; update `SessionData` and `NetworkMessages` for new data shape; update `CarSpawner` to handle dynamic color/model selection via attributes
- **Success signal**: A CSV with arbitrary columns (e.g., `teamName,language,accessibility_score,gender`) is parsed correctly, spawns cars, and attributes are accessible at runtime on `CarIdentity`

**Phase 2: Custom Rule Engine**
- **Goal**: Replace the hardcoded `EventMatcher` switch statement with a configurable rule engine that professors can parameterize
- **Scope**: Define `EventRule` data model (attribute name, comparison operator, comparison value, speed delta, duration); implement `RuleEngine.Evaluate(EventRule, CarIdentity)` supporting operators: equals, not_equals, contains, greater_than, less_than, is_true, is_false; update `EventManager` to work with `EventRule` list instead of `RaceEventConfig[]`; ensure all 7 v1 event types are reproducible as configured rules
- **Success signal**: A professor-configured rule "if attribute 'language' contains 'english', apply +10 speed for 6s" works identically to the old hardcoded FunctionBoost

**Phase 3: Survey Config System**
- **Goal**: Define a portable, human-readable configuration format for surveys, attribute mappings, and event rules
- **Scope**: Design `SurveyConfig` schema (JSON) with: question list (text, type, options, validation), attribute mappings (question → attribute name, transform rules), event rules (from Phase 2); implement `SurveyConfigManager` for save/load/export/import; create 2-3 built-in templates (v1 parity config, accessibility survey, general diversity survey)
- **Success signal**: A `SurveyConfig` JSON file can be loaded at startup, and the race uses the defined questions, mappings, and rules exactly as configured

**Phase 4: Professor Builder UI**
- **Goal**: Runtime UI enabling professors to create/edit surveys and rules without touching files
- **Scope**: Survey Builder panel (add/remove/reorder questions, set question type, configure options); Attribute Mapping panel (link questions to car attributes, set transform rules); Event Rule Builder panel (create/edit/delete rules with dropdowns for attribute, operator, value, effect); Config management (save, load, new, template selection); integrate with Setup Screen flow
- **Success signal**: A professor with no technical background can create a complete survey + rule configuration using only the in-game UI in under 15 minutes

**Phase 5: Student Survey System**
- **Goal**: Students answer survey questions directly in their browser, responses generate cars
- **Scope**: Student-facing survey UI (renders questions from SurveyConfig, supports text/choice/number inputs); WebSocket message types for survey distribution and response collection; `SurveyCollector` on professor-side to aggregate responses; response-to-CarData pipeline (apply attribute mappings to generate CarData from responses); real-time response counter on professor Setup Screen
- **Success signal**: Student opens browser with room code, sees survey questions, submits answers; professor sees response count increment; when race starts, student's answers drive their team's car attributes

**Phase 6: Integration & Testing**
- **Goal**: All systems work together end-to-end, with documentation for new users
- **Scope**: End-to-end testing (professor creates survey → students answer → race runs with custom rules → results export includes custom attributes); edge cases (empty responses, disconnections mid-survey, 50-car stress test with custom rules); migration guide for v1 users; update README and deployment docs; update SessionData to persist SurveyConfig
- **Success signal**: Complete workflow runs smoothly: custom survey → 30 student responses via WebSocket → race with custom events → CSV export with all custom attributes

### Parallelism Notes

- Phase 1 (Data Model) must complete first — it's the foundation everything builds on
- Phases 2 (Rule Engine) and 3 (Config System) can run in parallel after Phase 1, as they are independent systems: rules operate on the new data model, config defines the schema
- Phases 4 (Professor UI) and 5 (Student Survey) can run in parallel: professor UI needs Phases 2+3, student survey needs Phases 1+3
- Phase 6 (Integration) waits for everything

---

## Decisions Log

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Data model approach | Dynamic attributes (Dictionary) | Additional fixed fields, ECS-style components | Maximum flexibility; any survey question maps to an attribute without code changes |
| CSV backward compatibility | Breaking change (new format with headers) | Auto-detect old/new format | User accepted breaking change; simpler implementation; old format trivially convertible |
| Rule engine approach | Interpreted rules at runtime | Code generation, expression trees | Simplest to implement; performance is not a bottleneck (rules evaluated per-event, not per-frame) |
| Config format | JSON files | YAML, ScriptableObjects, database | Human-readable; works in WebGL; portable; no additional dependencies |
| Survey transport | Existing WebSocket infrastructure | New REST API, polling | WebSocket already handles room management and real-time sync; no new server needed |
| UI framework | Unity UGUI (existing) | UI Toolkit, third-party | Project already uses UGUI consistently; minimal new learning |
| Question types | Text, Multiple Choice, Numeric | Ranking, Matrix, File Upload | Covers 95% of EDI survey needs; complex types add UI complexity without proportional value |
| Attribute typing | String storage with typed accessors | Strongly typed attributes | Simplifies serialization; type coercion handled at access time |
| MVP scope | All features in v2.1 | Phased across v2.1/v2.2 | User wants full solution; no deadline pressure allows quality implementation |

---

## Research Summary

**Market Context**
- No competing product combines custom survey-to-game-mechanic mapping with EDI education — genuine whitespace confirmed across all major edu-game platforms
- Closest conceptual analogues: Privilege Walk simulations (static, not game-engine), Modified Monopoly (manual rules), Classcraft/TeachQuest (behavior-to-RPG mapping, but manual entry, no CSV import — Classcraft discontinued independently 2024)
- Competitor gap analysis: Kahoot/Blooket/Gimkit = quiz-correctness only; Mentimeter/Slido = polls without game mechanics; PhET/Labster = domain-locked science sims; none accept arbitrary external data mapped to game agents

| Tool | Custom Data Import | Data-to-Game Mapping | Real-time Professor Control |
|------|--------------------|----------------------|-----------------------------|
| Kahoot | No | No | No |
| Mentimeter | No (own polls only) | Partial (charts) | No |
| Classcraft (discontinued) | No (manual) | Yes (behavior→RPG) | Limited |
| PhET/Labster | No | Yes (domain-locked) | Yes (sliders) |
| **EDI Racing** | **Yes (CSV)** | **Yes (configurable)** | **Yes (live events)** |

- **Biggest market opportunity**: visual interface where professors drag CSV columns onto game parameters — no existing tool provides this for game-based visualization
- Best survey builder UX patterns (from Typeform, Google Forms, Jotform): one-question-at-a-time for engagement, drag-and-drop reordering, progress indicators ("Step 2 of 5"), templates as starting points, response rates drop sharply after 7 questions
- Anti-patterns: overwhelming configuration screens, requiring technical knowledge for basic setup, showing all options at once without progressive disclosure

**Technical Context**
- Unity's `JsonUtility` does not support `Dictionary<K,V>` — requires workaround (serializable key-value list) or Newtonsoft.Json
- Current codebase uses no namespaces, plain MonoBehaviours/ScriptableObjects, C# `Action<T>` events — new code must follow these conventions
- WebSocket infrastructure (NetworkManager + Node.js server) already handles room management, message relay, and reconnection — survey messages fit naturally
- CarController.ApplySpeedModifier(delta, duration) is already generic enough for custom rules — no changes needed to the speed modification API
- **WebGL survey UX**: use native Unity Canvas UI for buttons/toggles/sliders (multiple choice, rating scales); use HTML overlay via `.jslib` plugin for text input (Unity WebGL mobile text input is buggy on iOS Safari); Custom WebGL Templates in `Assets/WebGLTemplates/` for HTML alongside Unity canvas
- Related pedagogy tools validated the "unequal starting conditions → visual race outcome" metaphor: Privilege Walk, Equality of Life tabletop game (SAGE Journals 2025), Intergroup Monopoly (APA-endorsed)

---

*Generated: 2026-07-11*
*Status: READY FOR IMPLEMENTATION*
