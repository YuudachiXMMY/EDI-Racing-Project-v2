# Prod Ithaca — CI Build & Deploy

How the Ithaca server gets a ready-made Unity WebGL build without running Unity
locally. GitHub Actions builds the WebGL player and publishes it as a **rolling
GitHub Release**; the server downloads that release and rebuilds its Docker image.

- Build workflow: [`.github/workflows/prod-ithaca-build.yml`](../../.github/workflows/prod-ithaca-build.yml)
- Build entry point: [`Assets/Scripts/Editor/BuildScript.cs`](../../Assets/Scripts/Editor/BuildScript.cs) → `BuildScript.BuildWebGL`

---

## Pipeline at a glance

```
merge PR into prod/ithaca
        │
        ▼  (push to prod/ithaca)
GitHub Actions: prod-ithaca-build.yml
  1. free disk space
  2. checkout (with LFS)
  3. restore Library cache
  4. game-ci/unity-builder → BuildScript.BuildWebGL → Deploy/webgl-build/
  5. zip contents → ediracing-webgl.zip
  6. publish rolling release  tag=prod-ithaca-latest
        │
        ▼  (server pulls)
Ithaca server: /Users/jadyn/Development/IthacaServer
  gh release download prod-ithaca-latest → Deploy/webgl-build/
  docker compose build && up
```

The release asset contains **only** the WebGL player (`index.html`, `Build/`,
`TemplateData/`) — the expensive part CI produces. Everything else the server
needs (`Server/`, `Deploy/nginx`, `Deploy/Dockerfile`, compose files) comes from
a normal `git` checkout of `prod/ithaca`.

---

## One-time setup: Unity license

`game-ci/unity-builder` cannot build without a Unity license. Pick **one** path.

### Option A — Free Personal license (recommended for this project)

GameCI's old `unity-request-activation-file` action is **deprecated**. The current
method activates the license **locally in Unity Hub** and copies the resulting
`.ulf` into a secret. Since this machine already has Unity Hub + the editor, this
is a two-minute, one-time task.

1. Open **Unity Hub** → **Settings/Preferences → Licenses** → **Add** →
   **"Get a free personal license"**. Sign in with your Unity account if prompted.
2. Locate the generated `.ulf` file (Unity writes it on activation):
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. Store the secrets (from the repo root, `gh` authenticated):
   ```bash
   gh secret set UNITY_LICENSE < "/Library/Application Support/Unity/Unity_lic.ulf"
   gh secret set UNITY_EMAIL --body "your-unity-email"
   gh secret set UNITY_PASSWORD --body "your-unity-password"
   ```
   Or via UI: **Settings → Secrets and variables → Actions → New repository
   secret**, pasting the **entire contents** of the `.ulf` (including the XML
   header) into `UNITY_LICENSE`.

> Reference: <https://game.ci/docs/github/activation>. A Personal `.ulf` is tied to
> the account; if builds later fail with a license error, re-activate in Unity Hub
> and refresh the `UNITY_LICENSE` secret.
>
> **Note:** a `.ulf` may not exist even if Unity Hub shows an active license — you
> must complete the "Get a free personal license" flow above to materialize the file.

### Option B — Plus / Pro seat

Create these repository secrets instead of `UNITY_LICENSE`:

- `UNITY_SERIAL` = your Plus/Pro serial (e.g. `XX-XXXX-XXXX-XXXX-XXXX-XXXX`).
- `UNITY_EMAIL` = your Unity account email.
- `UNITY_PASSWORD` = your Unity account password.

The build workflow already wires all of these env vars; it uses whichever set you
provide. The first step of the workflow fails fast with a clear message if none
are configured.

---

## Create the branch and trigger the first build

```bash
# from the repo root, once
git checkout -b prod/ithaca
git push -u origin prod/ithaca
```

Every subsequent merge (push) into `prod/ithaca` triggers a build. You can also
re-run manually from the **Actions** tab (`workflow_dispatch`) without a push.

After a successful run, confirm the release exists:

```bash
gh release view prod-ithaca-latest
```

---

## Server side: pull the build and deploy

Run this on the Ithaca server (`/Users/jadyn/Development/IthacaServer`) once you
have cloned/checked out this repo there. It refreshes source **and** the build:

```bash
set -e
cd /Users/jadyn/Development/IthacaServer   # this repo, on the prod/ithaca branch

# 1. Latest server source (Server/, Deploy/nginx, Dockerfile, compose ...)
git fetch origin
git checkout prod/ithaca
git pull --ff-only

# 2. Latest WebGL build from the rolling release
rm -rf Deploy/webgl-build
mkdir -p Deploy/webgl-build
gh release download prod-ithaca-latest -p 'ediracing-webgl.zip' -D /tmp --clobber
unzip -q /tmp/ediracing-webgl.zip -d Deploy/webgl-build
test -f Deploy/webgl-build/index.html   # sanity check

# 3. Rebuild and restart (production overlay = Traefik TLS edge)
export INTERNAL_SECRET="$(cat /path/to/internal-secret)"   # openssl rand -hex 32, stored once
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml build
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

`gh` must be authenticated on the server once: `gh auth login` (or set
`GH_TOKEN`). A private repo requires a token with `repo` scope to download release
assets.

You'll wire the above into whatever deploy script lives in the server project.
A minimal `deploy-ediracing.sh` can be the three blocks above verbatim.

---

## Notes & caveats

- **Unity image availability.** `unity-builder@v4` pulls `unityci/editor` for the
  exact editor version (`6000.3.19f1`). If the image doesn't exist yet for a very
  new patch release, the build fails to pull. Workarounds: bump the project to a
  covered patch, or pin `customImage:` to the nearest published tag. Check tags at
  <https://hub.docker.com/r/unityci/editor/tags>.
- **Rolling tag.** `prod-ithaca-latest` is deleted and recreated each run so the
  download URL stays stable and always points at the newest build. There is no
  per-build history by design. If you want retained history, add a second
  versioned tag (e.g. `prod-ithaca-#<run_number>`) in the publish step.
- **Build scope.** The build includes a single scene
  (`Assets/Scenes/complete_track_demo.unity`, see `BuildScript.cs`). Update
  `BuildScript.BuildWebGL` to change the scene list.
- **First build is slow.** No Library cache yet + Unity image pull can take
  15–30 min. Subsequent builds reuse the cache and are much faster.
