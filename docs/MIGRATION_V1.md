# Migrating from v1 CSV Format

## What Changed

v2 uses a **header row** in CSV files. The first row defines column names; the first column must be `teamName`.

## Converting Your v1 CSV

**v1 format** (no header):
```
Alpha,2,password/glasses
Beta,3,facerecog
```

**v2 format** (add this header as the first line):
```
teamName,colorIndex,functions
Alpha,2,password/glasses
Beta,3,facerecog
```

That's it. Add one line and your existing CSV files work.

## Using the V1 Parity Template

To reproduce all 7 original ENGG*1100 events:

1. Launch the game → Setup Screen
2. Click **"Load Template"** → Select **"V1 Parity"**
3. Import your CSV (with header row added)
4. Start race — all original keyboard triggers (1-7) work as before

## New Capabilities

With v2 you can now:

- **Add any columns** — `teamName,language,accessibility_score,work_hours,...`
- **Create custom event rules** — "if language != English, apply -10 speed for 8s"
- **Collect data via student survey** — students answer on their phones, no CSV needed
- **Save/share configurations** — export your survey + rules as JSON for next semester

## Column Names

- Column names become attribute keys (case-insensitive)
- First column is always the team name (header text doesn't matter, but use `teamName` for clarity)
- No limit on number of columns
- Values are stored as strings; numeric comparisons parse at evaluation time
