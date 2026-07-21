# EDI Survey Web App

## Problem Statement

Unity 内的问卷系统（SurveyBuilderPanel、StudentSurveyPanel、SurveyCollector 等约 1500 行 MonoBehaviour 代码）维护成本高、迭代困难，且学生必须等待 Unity WebGL 加载完毕才能答题。将问卷功能分离到独立 Web App 后，开发维护更简单，教授和学生的使用体验更好（手机直接答题），同时问卷数据可跨 session 持久化复用。

## Evidence

- 当前问卷 UI 全部用 C# 手动构建（`SurveyBuilderPanel.cs` 503 行、`StudentSurveyPanel.cs` 496 行），每次 UI 调整需要重新构建 Unity WebGL
- 学生答题依赖 WebSocket 实时连接 + Unity WebGL 加载，在移动端体验差
- 问卷数据存储在 `Application.persistentDataPath` 文件系统中，WebGL 环境下不可靠，无法跨 session 复用
- 项目即将进入下一阶段，需要更可维护的架构

## Proposed Solution

构建一个独立的 Web App（React + SurveyJS + Node.js/Express + SQLite），教授在 Web 端完成**全部**配置工作：创建问卷题目（SurveyQuestion）、配置 attribute mapping（AttributeMapping）、以及设计赛事规则（SavedEventRule，包含速度修改、天气效果、触发条件等）。学生通过链接在手机/浏览器上答题。Web App 导出包含 `CarData[]` + `SavedEventRule[]` 的完整 JSON，Unity 端仅负责导入数据并运行比赛，不再提供任何配置界面。两个服务通过 Docker Compose 统一部署。

## Key Hypothesis

我们相信自动化的 Web 问卷配置流程将为教授增加配置和使用本项目的稳定性。
当教授可以在 10 分钟内完成配置并且系统稳定运行时，我们就知道做对了。

## What We're NOT Building

- **WebSocket 实时推送**（学生提交 → 赛车实时出现）— 增加复杂度，不符合课堂异步工作流
- **移动端原生 App** — Web App 响应式设计已满足手机使用
- **学生端用户认证系统** — 学生只需填写邮箱即可答题，降低使用门槛
- **Unity 内的问卷 UI** — 迁移后 Unity 端的 `SurveyBuilderPanel`、`StudentSurveyPanel`、`SurveyCollector` 将被移除

## Success Metrics

| Metric | Target | How Measured |
|--------|--------|--------------|
| 教授配置时间 | < 10 分钟（从创建问卷到可以开始比赛） | 计时测试 |
| 学生答题完成率 | > 95%（开始答题到提交成功） | Web App analytics |
| 移动端可用性 | 在主流手机浏览器上流畅运行 | 手动测试 iPhone/Android Chrome |
| Docker 部署成功率 | 一次 `docker compose up` 完成全部部署 | 部署脚本测试 |
| 系统稳定性 | 50 名学生同时答题无错误 | 负载测试 |

## Open Questions

- [ ] 技术栈最终选择：SurveyJS（MIT）还是自建表单系统？ Answer: SurveyJS
- [ ] 数据库选择：SQLite（轻量）还是 PostgreSQL（可扩展）？ Answer: SQLite
- [ ] 教授认证方案：简单密码保护、OAuth、还是 JWT？ Answer: 简单密码保护
- [ ] Unity 端数据获取方式：REST API 自动拉取还是手动导入 JSON？ Answer: Both
- [ ] 现有 Unity 端 survey 代码是否完全移除还是保留为 fallback？ Answer: 保留为 fallback
- [ ] Web App 是否需要支持多语言（英文/中文）？ Answer: 暂不，目前以纯英为主

---

## Users & Context

**Primary User — Professor**
- **Who**: 大学教授，教 EDI（Equity, Diversity & Inclusion）相关课程，技术水平一般
- **Current behavior**: 在 Unity WebGL 界面中手动创建问卷、等学生通过 WebSocket 连接后分发问卷、收集数据后启动比赛
- **Trigger**: 学期初需要为 EDI 课程准备赛车演示
- **Success state**: 10 分钟内完成问卷创建 + 学生答题链接分享 + 数据就绪可开始比赛

**Secondary User — Student**
- **Who**: 大学生，使用手机或笔记本浏览器
- **Current behavior**: 进入 Unity WebGL 页面 → 等待加载 → 加入 room → 等教授分发问卷 → 答题
- **Trigger**: 教授在课堂上要求填写问卷
- **Success state**: 点击链接 → 填写邮箱 → 答题 → 提交完成（< 3 分钟）

**Job to Be Done**
当有教授想要开设 EDI Racing 课程时，教授可以根据手册低成本地配置好问卷并连接到 EDI Racing，这样教授就可以直接在课堂上使用。

**Non-Users**
- 游戏开发者（不提供可视化编辑器或引擎插件接口）
- 需要复杂分支逻辑问卷的研究人员（问卷保持简单：Text/MultipleChoice/Numeric）

---

## Solution Detail

### Core Capabilities (MoSCoW)

| Priority | Capability | Rationale |
|----------|------------|-----------|
| Must | 教授问卷创建器（Questions + Mappings + Rules） | 核心功能，替代 Unity SurveyBuilderPanel |
| Must | 学生答题页面（手机友好） | 核心功能，替代 Unity StudentSurveyPanel |
| Must | 数据导出（JSON 格式，兼容 Unity CarData + SavedEventRule） | 桥接 Web App 和 Unity |
| Must | 教授认证（登录/注册） | 保护问卷配置数据 |
| Must | Docker Compose 统一部署（Web App + Unity WebGL） | 运维目标 |
| Must | 问卷模板（V1 Parity, Accessibility, Diversity） | 迁移现有 SurveyTemplates |
| Should | REST API 供 Unity 直接拉取数据 | 减少手动步骤 |
| Should | 问卷分享链接（学生通过链接访问） | 简化学生入口 |
| Should | 数据持久化（跨 session 复用问卷和数据） | 教授不需每次重建 |
| Could | 问卷回答实时统计面板 | 教授可看到多少学生已答 |
| Could | 结果历史记录和对比 | 跨学期数据分析 |
| Won't | WebSocket 实时推送（答题 → 赛车实时出现） | 明确排除，不符合异步工作流 |
| Won't | 学生认证系统 | 降低使用门槛，邮箱即可 |

### MVP Scope

教授可以在 Web App 上：
1. 注册/登录
2. 创建问卷（题目 + attribute mapping + event rules）或从模板开始
3. 生成学生答题链接
4. 查看回答数量
5. 导出 JSON 文件（包含 `CarData[]` + `SavedEventRule[]`）

学生可以在 Web App 上：
1. 通过链接访问问卷
2. 填写邮箱 + team name
3. 答题并提交

Unity 端：
1. 教授在 SetupScreen 导入 JSON（替代现有 CSV/Survey 流程）
2. `CsvParser` 或新的 `JsonImporter` 解析为 `List<CarData>`
3. Event rules 自动应用到 `EventSchedule`

### User Flow

```
教授:
  Web App 登录 → 创建/选择问卷 → 配置 Mappings + Rules
    → 生成学生链接 → 等待学生答题 → 导出 JSON
    → Unity 导入 JSON → 开始比赛

学生:
  收到链接 → 打开页面 → 填邮箱 + team name → 答题 → 提交 → 完成
```

---

## Technical Approach

**Feasibility**: HIGH

**Architecture Notes**

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Compose                            │
│                                                             │
│  ┌──────────────────────┐    ┌────────────────────────────┐ │
│  │   EDI Survey Web App │    │   EDI Racing (Unity WebGL) │ │
│  │                      │    │                            │ │
│  │  ┌────────────────┐  │    │  ┌──────────┐ ┌─────────┐ │ │
│  │  │ React Frontend │  │    │  │ WebGL    │ │ nginx   │ │ │
│  │  │ (SurveyJS)     │  │    │  │ Build    │ │         │ │ │
│  │  └───────┬────────┘  │    │  └──────────┘ └─────────┘ │ │
│  │          │           │    │                            │ │
│  │  ┌───────┴────────┐  │    │  ┌──────────────────────┐ │ │
│  │  │ Express API    │──┼────┼──│ WebSocket Server     │ │ │
│  │  │ (Node.js)      │  │    │  │ (room relay)         │ │ │
│  │  └───────┬────────┘  │    │  └──────────────────────┘ │ │
│  │          │           │    │                            │ │
│  │  ┌───────┴────────┐  │    └────────────────────────────┘ │
│  │  │ SQLite/Postgres│  │                                   │
│  │  └────────────────┘  │                                   │
│  └──────────────────────┘                                   │
└─────────────────────────────────────────────────────────────┘
```

- **前端**: React + SurveyJS Creator（教授端）+ SurveyJS Runner（学生端）
- **后端**: Node.js + Express（REST API）
- **数据库**: SQLite（MVP）或 PostgreSQL（生产）
- **认证**: JWT for 教授；学生无认证（邮箱作为标识）
- **导出格式**: JSON，schema 与现有 `SurveyConfig` 完全兼容
- **Unity 集成**: 新增 `JsonImporter` 类解析 Web App 导出的 JSON → `List<CarData>` + `SavedEventRule[]`

**Data Schema（Web App ↔ Unity 共享）**

```json
{
  "configName": "Accessibility Survey",
  "carData": [
    {
      "teamName": "Team Alpha",
      "attributes": [
        { "key": "disability", "value": "physical" },
        { "key": "assistive_tech", "value": "yes" }
      ]
    }
  ],
  "eventRules": [
    {
      "displayName": "Inaccessible Building",
      "attributeName": "disability",
      "operator": 1,
      "compareValue": "none",
      "speedDelta": -12.0,
      "duration": 8.0,
      "weather": 0,
      "allowRepeat": false
    }
  ]
}
```

**Technical Risks**

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| SurveyJS 功能不足以支持 Mapping/Rules 编辑 | M | SurveyJS 只负责问卷题目；Mapping/Rules 用自定义 React 组件 |
| JSON schema 不兼容导致 Unity 导入失败 | L | 共享 schema 定义；Unity 端加 validation |
| SQLite 并发限制（50 学生同时提交） | L | WAL mode + connection pooling；必要时升级 PostgreSQL |
| Docker 镜像体积过大 | L | 多阶段构建；Web App 和 Unity 分离镜像 |

---

## Implementation Phases

| # | Phase | Description | Status | Parallel | Depends | PRP Plan |
|---|-------|-------------|--------|----------|---------|----------|
| 1 | Web App 基础架构 | Express API + SQLite + 教授认证 + Docker 化 | complete | - | - | `.claude/PRPs/reports/web-app-foundation-report.md` |
| 2 | 问卷创建器 | React + SurveyJS Creator → 题目/Mapping/Rules 编辑 | complete | - | 1 | `.claude/PRPs/reports/survey-creator-report.md` |
| 3 | 学生答题端 | SurveyJS Runner + 邮箱/team name + 响应式移动端 | complete | with 4 | 2 | `.claude/PRPs/reports/student-survey-page-report.md` |
| 4 | 数据导出 + Unity 集成 | JSON 导出 API + Unity JsonImporter + SetupScreen 改造 | complete | with 3 | 2 | `.claude/PRPs/reports/data-export-unity-integration-report.md` |
| 5 | 模板迁移 + Docker 统一部署 | 迁移 SurveyTemplates + docker-compose 统一编排 | complete | - | 3, 4 | `.claude/PRPs/reports/template-migration-docker-unified-deploy-report.md` |
| 6 | Unity 端清理 | 移除 SurveyBuilderPanel/StudentSurveyPanel/SurveyCollector | complete | - | 5 | `.claude/PRPs/reports/unity-survey-cleanup-report.md` |

### Phase Details

**Phase 1: Web App 基础架构**
- **Goal**: 搭建 Web App 骨架，教授可以注册/登录
- **Scope**: Express 项目初始化、SQLite schema（users, surveys, responses）、JWT 认证、Docker 化
- **Success signal**: 教授可以注册、登录、看到空的 dashboard

**Phase 2: 问卷创建器**
- **Goal**: 教授可以创建完整的 SurveyConfig（问题 + Mapping + Rules）
- **Scope**: SurveyJS Creator 集成（Questions tab）、自定义 Mapping 编辑器、自定义 Rules 编辑器、保存/加载问卷
- **Success signal**: 教授创建的问卷 JSON 与现有 `SurveyConfig` schema 兼容

**Phase 3: 学生答题端**
- **Goal**: 学生通过链接在手机上答题
- **Scope**: 问卷分享链接生成、SurveyJS Runner 渲染、邮箱 + team name 输入、提交 + 确认页面、移动端响应式
- **Success signal**: 学生在手机 Chrome 上 3 分钟内完成答题

**Phase 4: 数据导出 + Unity 集成**
- **Goal**: Web App 导出的 JSON 可以直接被 Unity 导入并开始比赛
- **Scope**: REST API `GET /api/surveys/:id/export`、Unity 端 `JsonImporter` 类、SetupScreen 增加 "Import from Web App" 按钮
- **Success signal**: 教授在 Web App 导出 → Unity 导入 → 赛车正确生成并带有正确属性和事件规则

**Phase 5: 模板迁移 + Docker 统一部署**
- **Goal**: 一次 `docker compose up` 启动全部服务
- **Scope**: 迁移 3 个 SurveyTemplates 到 Web App seed data、docker-compose.yml 统一编排（Web App + Unity WebGL + WebSocket）
- **Success signal**: 全新机器上 `docker compose up` 后教授可以创建问卷并启动比赛

**Phase 6: Unity 端清理**
- **Goal**: 移除 Unity 中不再需要的 survey 代码
- **Scope**: 移除 `SurveyBuilderPanel`, `StudentSurveyPanel`, `SurveyCollector`, `SurveyConfigManager`（仅保留 ApplyRulesToSchedule 逻辑）、清理 `NetworkSync` 中的 survey 消息处理、清理 `NetworkMessages` 中的 survey 消息类型
- **Success signal**: Unity 编译通过，所有 survey 功能在 Web App 中完成

### Parallelism Notes

Phase 3（学生答题）和 Phase 4（导出+Unity集成）可以并行：前者是 Web 前端工作，后者涉及 API 端点和 Unity C# 开发，无代码依赖。

---

## Decisions Log

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| 前端框架 | React + SurveyJS | Vue, Angular, 纯 HTML | SurveyJS 有成熟 React 组件；MIT 协议；JSON-native 匹配现有数据模型 |
| 后端框架 | Node.js + Express | Python FastAPI, Go | 项目已有 Node.js WebSocket 服务器，统一技术栈；教授/社区更容易维护 |
| 数据库 | SQLite（MVP）→ PostgreSQL（生产） | MongoDB, MySQL | SQLite 零配置适合 MVP 和 Docker 部署；PostgreSQL 成熟稳定可选升级 |
| 认证方案 | JWT（教授）+ 无认证（学生） | OAuth, Session-based | 最简方案；学生零门槛；教授只需邮箱+密码 |
| 数据传输 | JSON 文件导出 + REST API | WebSocket 推送, CSV | JSON 与现有 SurveyConfig 1:1 映射；REST 适合异步工作流 |
| 部署 | Docker Compose 统一编排 | 分离部署, K8s | 与现有 Deploy/ 目录一致；单机部署最简 |
| Unity 集成方式 | JSON 导入（手动/API） | CSV 导入, WebSocket | 保持简单；现有 CsvParser 模式可扩展为 JSON |

---

## Research Summary

**Market Context**
- 没有现成的 "survey → game parameter" 产品，这是本项目独有需求
- SurveyJS（MIT）是最佳基础：问卷定义即 JSON schema，可嵌入自定义 Web App
- Formbricks 是备选（AGPLv3，较重）
- 类似项目 InGameSurvey（Azure Functions + Unity）方向相反（游戏内调查，非调查驱动游戏）

**Technical Context**
- 现有 Unity 数据模型（SurveyConfig, SurveyQuestion, AttributeMapping, SavedEventRule）全部是 `[Serializable]` 结构，与 JSON 1:1 映射
- `SurveyResponseMapper.MapResponses()` 是纯静态方法，可直接在 Web App 后端用 JS 重新实现
- 现有 Docker 基础设施（`Deploy/docker-compose.yml`）可扩展
- `CsvParser` 和 `CarData` 在 Unity 端保留，新增 JSON 导入路径

---

*Generated: 2026-07-12*
*Status: DRAFT - needs validation*
