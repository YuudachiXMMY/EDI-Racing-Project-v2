# Plan: WebGL Build & Docker Deployment

## Summary
Configure the Unity project for optimized WebGL builds with Brotli compression, create a custom WebGL template that auto-connects to the WebSocket server, and package everything into a single Docker container running nginx (static files) + Node.js (WebSocket relay). A `docker-compose.yml` and build automation script enable one-command deployment.

## User Story
As a professor, I want to run `docker-compose up` and have the complete racing game accessible at `localhost:8080`, so that I can deploy the EDI Racing Game for classroom use without any manual setup steps.

## Problem → Solution
Currently the WebGL build, WebSocket server, and static file serving are separate manual steps with no unified deployment → A single Docker container with nginx reverse-proxying to the Node.js WebSocket server, serving the WebGL build, accessible via one URL.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-racing-v2.prd.md`
- **PRD Phase**: Phase 6 — WebGL Build & Docker
- **Estimated Files**: 8-10 files

---

## UX Design

### Before
```
Professor:
1. Open Unity Editor
2. Build WebGL manually (File > Build)
3. Start WebSocket server manually (cd Server && npm start)
4. Set up a local web server for the build output
5. Tell students the URL (and it might not work behind firewalls)

Student:
1. Receives URL from professor
2. Hope that CORS, compression, and WebSocket routing all work
```

### After
```
Professor:
1. Place WebGL build in Deploy/webgl-build/ (or build via script)
2. Run: docker-compose up
3. Open browser to localhost:8080 → Professor view
4. Share URL with students (same host, add ?role=student&room=XXXXXX)

Student:
1. Opens provided URL
2. WebGL game loads, enters room code
3. Spectates race
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Server startup | Manual: 2 separate processes | `docker-compose up` | Single command |
| WebGL serving | No built-in server | nginx with Brotli support | Content-Encoding headers |
| WebSocket URL | Hardcoded `ws://localhost:8080` | Auto-detected from page URL | `ws(s)://hostname/ws` |
| Student access | Same machine only | Any device on network | Docker exposes port 8080 |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Server/server.js` | all | WebSocket server to integrate with nginx |
| P0 (critical) | `Server/package.json` | all | Node.js dependencies |
| P0 (critical) | `Assets/Plugins/WebGL/WebSocketBridge.jslib` | all | WebGL-side WebSocket — URL comes from C# |
| P0 (critical) | `Assets/Scripts/Network/NetworkManager.cs` | 8-17 | `ServerUrl` field — must be dynamic |
| P1 (important) | `Assets/Scripts/Data/SessionManager.cs` | all | Uses `File.WriteAllText` — breaks in WebGL |
| P1 (important) | `ProjectSettings/ProjectSettings.asset` | 801-826 | Current WebGL settings to optimize |
| P2 (reference) | `Assets/Scripts/UI/JoinScreen.cs` | all | Student join flow |
| P2 (reference) | `Assets/Scripts/UI/SetupScreen.cs` | all | Professor setup flow |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity 6 WebGL build | Unity docs | Brotli compression = `webGLCompressionFormat: 1`; decompression fallback needed if nginx serves pre-compressed |
| nginx Brotli module | nginx docs | Use `brotli_static on` to serve `.br` files; set correct Content-Encoding and MIME types |
| WebSocket proxy | nginx docs | `proxy_pass http://localhost:WS_PORT; proxy_http_version 1.1; proxy_set_header Upgrade $http_upgrade;` |
| Docker multi-stage | Docker docs | Use `node:20-alpine` for server; `nginx:alpine` as final with Node.js sidecar |
| Unity WebGL IndexedDB | Unity docs | `Application.persistentDataPath` maps to IndexedDB in WebGL — `File.*` APIs work but async |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
// SOURCE: Server/server.js:1-10, Server/package.json
```
- Server scripts: plain JS (CommonJS require), no TypeScript
- Package name: kebab-case (edi-racing-server)
- Config: environment variables with defaults (PORT = process.env.PORT || '8080')
```

### ERROR_HANDLING
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:136-138
```csharp
private void HandleError(string error)
{
    Debug.LogWarning($"[NetworkManager] Error: {error}");
    OnConnectionError?.Invoke(error);
}
```

### SERVER_PATTERN
// SOURCE: Server/server.js:65, 82-93
```javascript
const wss = new WebSocketServer({ port: PORT });
wss.on('connection', (ws) => {
    ws.isAlive = true;
    ws.on('pong', () => { ws.isAlive = true; });
    ws.on('message', (data) => { /* ... */ });
});
```

### WEBGL_PLATFORM_CHECK
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.cs:4-11
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
#endif
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Deploy/nginx/nginx.conf` | CREATE | nginx config: serve WebGL + reverse proxy WebSocket |
| `Deploy/nginx/mime.types` | CREATE | Custom MIME types for `.wasm`, `.data`, `.br` files |
| `Deploy/Dockerfile` | CREATE | Multi-process container: nginx + Node.js |
| `Deploy/docker-compose.yml` | CREATE | One-command deployment |
| `Deploy/start.sh` | CREATE | Entrypoint script to start both nginx and Node.js |
| `Deploy/.dockerignore` | CREATE | Exclude Unity project files from Docker context |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | Auto-detect WebSocket URL from page hostname in WebGL |
| `Assets/Scripts/Data/SessionManager.cs` | UPDATE | Guard `File.*` calls with `#if !UNITY_WEBGL` or add WebGL-safe fallback |
| `ProjectSettings/ProjectSettings.asset` | UPDATE | Optimize WebGL settings (compression, memory) |
| `Assets/WebGLTemplates/EDIRacing/index.html` | CREATE | Custom template with loading screen and auto-WS-URL |
| `scripts/build-webgl.sh` | CREATE | Automation script for Unity CLI batch build |

## NOT Building

- CI/CD pipeline (GitHub Actions) — deferred to Phase 7 polish
- HTTPS/WSS certificate management — localhost-only for v2.0
- CDN deployment — professor self-hosts via Docker
- Custom loading screen branding — use simple template
- Multi-container Docker Compose (keep single container for simplicity)
- WebGL build inside Docker (requires Unity license; build is done locally)

---

## Step-by-Step Tasks

### Task 1: Fix SessionManager WebGL Compatibility
- **ACTION**: Guard `System.IO` file operations that break in WebGL sandboxed environment
- **IMPLEMENT**: Wrap `SaveSession`, `LoadSession`, `ExportResults`, `FindLatestSession`, `GetSavedSessionPaths` with `#if UNITY_WEBGL && !UNITY_EDITOR` guards. In WebGL, use `Application.persistentDataPath` which maps to IndexedDB (Unity handles this), but log warnings for operations that may behave differently. Key concern: `Directory.CreateDirectory` and `File.WriteAllText` do work in WebGL (Unity's VFS layer), but `Directory.GetFiles` ordering may differ. The primary issue is that the professor cannot easily access exported files from WebGL — add a browser download alternative using the jslib.
- **MIRROR**: WEBGL_PLATFORM_CHECK pattern
- **IMPORTS**: `System.Runtime.InteropServices` (for DllImport in WebGL block)
- **GOTCHA**: Unity WebGL does support `System.IO` via Emscripten's virtual filesystem + IndexedDB sync, so the existing code *technically* works. However, exported CSV files are invisible to the user. Must add a JS-side file download for exports.
- **VALIDATE**: Build compiles for WebGL without errors; file operations still work in Editor

### Task 2: Auto-Detect WebSocket URL in NetworkManager
- **ACTION**: Replace hardcoded `ws://localhost:8080` with auto-detection from the page's hostname
- **IMPLEMENT**: In WebGL builds, use a jslib call to get `window.location` and construct `ws://hostname/ws`. In Editor/Standalone, keep the configurable `ServerUrl` field. Add a new jslib function `GetPageHost()` and call it from `NetworkManager.Awake()` or `Connect()` when running in WebGL.
- **MIRROR**: WEBGL_PLATFORM_CHECK, SERVER_PATTERN
- **IMPORTS**: `System.Runtime.InteropServices` (WebGL block)
- **GOTCHA**: `window.location.protocol` determines `ws://` vs `wss://`. Must handle both. Use `/ws` path for nginx reverse proxy routing.
- **VALIDATE**: In Editor, `ServerUrl` field is still used. In WebGL build, URL is derived from page host.

### Task 3: Update WebSocketBridge.jslib for Host Detection
- **ACTION**: Add `GetPageHost` function to the jslib
- **IMPLEMENT**: Add a new exported function that returns `window.location.host` as a string to C#. Also add `DownloadFile` function for Task 1's browser download.
- **MIRROR**: Existing jslib pattern in `Assets/Plugins/WebGL/WebSocketBridge.jslib`
- **IMPORTS**: None (pure JS)
- **GOTCHA**: Must use `_malloc`, `stringToUTF8`, and return pointer for string return values in jslib. Or use `SendMessage` callback pattern like existing code.
- **VALIDATE**: Functions are callable from C# in WebGL builds

### Task 4: Optimize WebGL Project Settings
- **ACTION**: Update `ProjectSettings/ProjectSettings.asset` for production WebGL builds
- **IMPLEMENT**: Changes to make:
  - `webGLCompressionFormat: 1` (Brotli, was 0=disabled)
  - `webGLDecompressionFallback: 1` (enable for browsers without Brotli)
  - `webGLNameFilesAsHashes: 1` (cache-friendly filenames)
  - `webGLExceptionSupport: 0` (disable for smaller/faster builds, was 1)
  - `webGLInitialMemorySize: 64` (increase from 32MB for 50 cars)
  - `webGLMaximumMemorySize: 2048` (keep as is)
  - `webGLPowerPreference: 2` (high performance, already set)
  - Code stripping: enable IL2CPP managed stripping level to "Medium" in Player Settings
- **MIRROR**: N/A — project settings format
- **IMPORTS**: N/A
- **GOTCHA**: Disabling exception support means no try-catch in WebGL runtime. All current code uses Debug.Log for errors, which is fine. `webGLDecompressionFallback` adds a JS decompressor as fallback — increases build size slightly but ensures Safari compatibility.
- **VALIDATE**: Settings file parses correctly; test build produces `.br` compressed files

### Task 5: Create Custom WebGL Template
- **ACTION**: Create `Assets/WebGLTemplates/EDIRacing/index.html` with loading bar and responsive canvas
- **IMPLEMENT**: Template must:
  - Full-viewport canvas (no scrollbars)
  - Simple loading progress bar
  - Meta viewport tag for mobile browsers
  - No external dependencies (self-contained HTML)
  - Pass `{{{ UNITY_LOADER_URL }}}`, `{{{ DATA_URL }}}`, `{{{ FRAMEWORK_URL }}}`, `{{{ CODE_URL }}}` Unity template variables
- **MIRROR**: Unity WebGL template conventions
- **IMPORTS**: N/A (HTML/JS)
- **GOTCHA**: Must include `thumbnail.png` in template folder for Unity Editor to recognize it. Template variable syntax is `{{{ VAR }}}` (triple braces). Need `decompressedSize` option for Brotli builds.
- **VALIDATE**: Template appears in Player Settings > WebGL > Resolution and Presentation > WebGL Template

### Task 6: Create nginx Configuration
- **ACTION**: Create `Deploy/nginx/nginx.conf` that serves WebGL build and proxies WebSocket
- **IMPLEMENT**:
  - Listen on port 80 (mapped to 8080 via Docker)
  - Serve static files from `/usr/share/nginx/html/` (WebGL build output)
  - Location `/ws` proxies to `localhost:3000` (Node.js WebSocket server, internal port)
  - Enable `gzip_static` and appropriate MIME types for `.wasm`, `.data`, `.js`
  - Set `Content-Encoding: br` for pre-compressed Brotli files
  - CORS headers for WebGL compatibility
  - WebSocket upgrade headers: `Upgrade`, `Connection`, `Host`
- **MIRROR**: NAMING_CONVENTION for config files
- **IMPORTS**: N/A
- **GOTCHA**: nginx must serve `.wasm` files with `application/wasm` MIME type. Brotli files need `Content-Encoding: br` header. WebSocket proxy needs `proxy_read_timeout` increased (default 60s drops idle connections).
- **VALIDATE**: `nginx -t` passes config validation

### Task 7: Update Server Port for Docker Internal Use
- **ACTION**: The WebSocket server should listen on port 3000 internally (nginx proxies from `/ws`)
- **IMPLEMENT**: No code change needed — `server.js` already reads `PORT` from env. Set `PORT=3000` in Docker environment. But update the `WebSocketBridge.jslib` default to use `/ws` path.
- **MIRROR**: SERVER_PATTERN
- **IMPORTS**: N/A
- **GOTCHA**: Server must NOT serve on the same port as nginx. Internal port 3000 is only accessible within the Docker container.
- **VALIDATE**: `PORT=3000 node server.js` starts correctly

### Task 8: Create Dockerfile
- **ACTION**: Create `Deploy/Dockerfile` with nginx + Node.js in a single container
- **IMPLEMENT**:
  - Base: `nginx:alpine`
  - Install Node.js via `apk add nodejs npm`
  - Copy `Server/` contents and run `npm ci --production`
  - Copy WebGL build to `/usr/share/nginx/html/`
  - Copy nginx config to `/etc/nginx/`
  - Copy `start.sh` entrypoint that runs both nginx and Node.js
  - Expose port 80
- **MIRROR**: NAMING_CONVENTION
- **IMPORTS**: N/A
- **GOTCHA**: WebGL build must be done BEFORE Docker build (Unity CLI build requires license). The Dockerfile expects the build output in `Deploy/webgl-build/`. Use `supervisord` or background process for running both nginx and Node.js. Simpler: `start.sh` runs Node.js in background, nginx in foreground.
- **VALIDATE**: `docker build -t edi-racing ./Deploy` succeeds

### Task 9: Create docker-compose.yml
- **ACTION**: Create `Deploy/docker-compose.yml` for one-command deployment
- **IMPLEMENT**:
  ```yaml
  services:
    edi-racing:
      build: .
      ports:
        - "8080:80"
      environment:
        - PORT=3000
      restart: unless-stopped
  ```
- **MIRROR**: NAMING_CONVENTION
- **IMPORTS**: N/A
- **GOTCHA**: Port 8080 on host maps to 80 in container (nginx). Professor and students all connect to `http://HOST:8080`.
- **VALIDATE**: `docker-compose up -d` starts container; `curl localhost:8080` returns HTML

### Task 10: Create Entrypoint Script
- **ACTION**: Create `Deploy/start.sh` that starts Node.js and nginx
- **IMPLEMENT**:
  ```bash
  #!/bin/sh
  # Start WebSocket server in background
  cd /app/server && node server.js &
  # Start nginx in foreground (PID 1)
  nginx -g 'daemon off;'
  ```
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: nginx must run in foreground (`daemon off`) so Docker container stays alive. Node.js runs in background. If Node.js crashes, nginx stays up but WebSocket fails — acceptable for classroom use.
- **VALIDATE**: Both processes run; WebSocket connections work alongside static file serving

### Task 11: Create .dockerignore
- **ACTION**: Create `Deploy/.dockerignore` to keep Docker context small
- **IMPLEMENT**: Exclude `Assets/`, `Library/`, `Logs/`, `Temp/`, `ProjectSettings/`, `.git/`, `node_modules/`, `*.meta`. Only include `Deploy/` contents and `Server/`.
- **MIRROR**: N/A
- **IMPORTS**: N/A
- **GOTCHA**: Docker context must include `Server/` for the Node.js code and `Deploy/webgl-build/` for the WebGL output.
- **VALIDATE**: `docker build` context size is small (<50MB)

### Task 12: Create Build Automation Script
- **ACTION**: Create `scripts/build-webgl.sh` that runs Unity CLI batch build
- **IMPLEMENT**:
  ```bash
  #!/bin/bash
  UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity}"
  PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
  BUILD_PATH="$PROJECT_PATH/Deploy/webgl-build"
  
  $UNITY_PATH -batchmode -nographics -projectPath "$PROJECT_PATH" \
    -executeMethod BuildScript.BuildWebGL \
    -buildTarget WebGL \
    -logFile - \
    -quit
  ```
  Also create a simple `Assets/Scripts/Editor/BuildScript.cs` with a `BuildWebGL` static method.
- **MIRROR**: Editor script pattern (Assets/Scripts/Editor/)
- **IMPORTS**: `UnityEditor`, `UnityEditor.Build`
- **GOTCHA**: Unity CLI requires an activated license. The script should detect Unity path automatically on macOS. Build output goes to `Deploy/webgl-build/`.
- **VALIDATE**: `./scripts/build-webgl.sh` produces WebGL build output

---

## Testing Strategy

### Unit Tests

This phase is infrastructure/deployment — no traditional unit tests. Validation is via integration testing.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| WebGL build compiles | Unity project | Build output in Deploy/webgl-build/ | Missing NavMesh agent type in WebGL |
| Docker container starts | `docker-compose up` | Container healthy, ports accessible | Port 8080 already in use |
| nginx serves index.html | `curl localhost:8080` | HTTP 200 with HTML content | |
| WebSocket proxy works | WS connect to `localhost:8080/ws` | Connection established | |
| Professor creates room | Open browser, click Host | Room code appears | WebSocket not ready yet |
| Student joins room | Enter room code | "Joined room" message | Invalid room code |
| Race sync works | Professor starts race | Student sees cars move | Late-joining student |
| Brotli files served | `curl -H "Accept-Encoding: br"` | Content-Encoding: br header | Browser without Brotli support |
| SessionManager in WebGL | Save session in WebGL build | Data persists in IndexedDB | Browser clears storage |

### Edge Cases Checklist
- [ ] Browser without Brotli support (decompression fallback enabled)
- [ ] Port 8080 already in use on host
- [ ] WebGL build larger than 50MB (optimize assets)
- [ ] Student connects before professor creates room
- [ ] Multiple professors on same server (room isolation)
- [ ] Browser tab close during race (WebSocket cleanup)
- [ ] Docker container restart (rooms lost, expected)
- [ ] Safari WebGL compatibility (test WASM support)

---

## Validation Commands

### Static Analysis
```bash
# No CLI type checker for C# — validate via Unity Editor compile
# Open Unity, check Console for errors after script changes
```
EXPECT: Zero compile errors in Unity Console

### Docker Build
```bash
cd Deploy && docker build -t edi-racing .
```
EXPECT: Image builds successfully

### Docker Run
```bash
cd Deploy && docker-compose up -d
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080
```
EXPECT: HTTP 200

### WebSocket Test
```bash
# Test WebSocket proxy
npx wscat -c ws://localhost:8080/ws
# Type: {"type":"create_room"}
# Expect: {"type":"room_created","roomCode":"XXXXXX"}
```
EXPECT: Room created response

### nginx Config Validation
```bash
docker exec edi-racing-edi-racing-1 nginx -t
```
EXPECT: "syntax is ok", "test is successful"

### Manual Validation
- [ ] Unity WebGL build compiles without errors
- [ ] `docker-compose up` starts container successfully
- [ ] Professor opens `http://localhost:8080` — WebGL game loads
- [ ] Professor clicks "Host" — room code appears
- [ ] Student opens `http://localhost:8080` on second browser/tab
- [ ] Student enters room code — joins successfully
- [ ] Professor starts race — cars appear on both screens
- [ ] Student sees cars moving (interpolated positions)
- [ ] Leaderboard updates on student view
- [ ] Professor triggers event — student sees event notification
- [ ] WebGL build size is under 50MB compressed
- [ ] Works in Chrome, Firefox, Safari, Edge

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] All validation commands pass
- [ ] WebGL build compiles and runs in browser
- [ ] Docker container serves game and WebSocket on single port
- [ ] `docker-compose up` is the only command needed to deploy
- [ ] Professor and student views work from separate browsers
- [ ] WebGL build size < 50MB compressed
- [ ] Brotli compression enabled and served correctly
- [ ] SessionManager doesn't crash in WebGL builds
- [ ] WebSocket URL auto-detected (no hardcoded localhost in builds)

## Completion Checklist
- [ ] Code follows discovered patterns (platform guards, env vars, jslib conventions)
- [ ] Error handling: NetworkManager logs warnings, nginx returns 502 if WS server down
- [ ] No hardcoded values (ports via env vars, URLs auto-detected)
- [ ] Docker-specific files in Deploy/ directory (clean separation)
- [ ] Build script documented in comments
- [ ] No unnecessary scope additions (no CI/CD, no HTTPS, no CDN)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| WebGL build too large (>50MB) | MEDIUM | Slow load for students | Strip unused assets, enable texture compression, profile build report |
| Unity CLI build requires license | HIGH | Can't automate without activated license | Document manual build as fallback; script is convenience only |
| nginx Brotli module not in alpine image | LOW | Need custom nginx build or gzip fallback | Use `gzip_static` as fallback; Brotli served via Unity's decompression fallback |
| SessionManager File I/O in WebGL | MEDIUM | Save/load/export may fail silently | Add WebGL-specific download via jslib for exports; test thoroughly |
| Safari WebGL issues | LOW | Some students can't participate | Test matrix; WebGL 2.0 supported in Safari 15+ |
| Docker networking on university networks | MEDIUM | Firewall blocks port 8080 | Document port configuration; suggest IT coordination |

## Notes
- The WebGL build must be done locally in Unity Editor (or CI with Unity license). The Docker container only packages the pre-built output.
- The `Deploy/` directory is the Docker build context. Structure:
  ```
  Deploy/
  ├── Dockerfile
  ├── docker-compose.yml
  ├── .dockerignore
  ├── start.sh
  ├── nginx/
  │   └── nginx.conf
  └── webgl-build/           ← Unity WebGL build output goes here
      ├── index.html
      ├── Build/
      │   ├── webgl-build.data.br
      │   ├── webgl-build.framework.js.br
      │   ├── webgl-build.loader.js
      │   └── webgl-build.wasm.br
      └── TemplateData/
  ```
- The `Server/` directory (at project root) contains the WebSocket relay code, copied into the Docker image during build.
- Port mapping: Host 8080 → Container 80 (nginx). nginx proxies `/ws` to container-internal port 3000 (Node.js).
- Room codes are ephemeral — restarting the Docker container clears all rooms. This is acceptable for classroom use (each class session is independent).
