# Backlog

## Now (next 2-4 weeks)
- Heuristics for project detection (markers: `.sln`, `.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`).
- GUI: Completed scan shouldn't have "Stop" button visible. It should have dismiss button or something like this.
- GUI: Active scans current path if dont fit into window it should have horizontal scroll.
- ETA doesn't work
- Pipeline: scan JSON -> `ProjectSuggestion` + save to SQLite + UI suggestions list.
- Root badges in Scan: project count + ongoing suggestions count.
- Scan summary per root (last state, time, files count).
- Queue visibility in Scan UI with `queue reason` (if still incomplete).
- Hide `Stop` button after scan is finished (confirm UX and close).
- Pre-commit: run tests and block commit when tests fail.

## Soon (1-3 months)
- Tag suggestions (heuristics first, no AI by default).
- Manual split/merge of detected projects inside a root.
- Better ranking and filtering of suggestions (score, language, age).
- Incremental scan based on `last_modified_at` + cache.
- Configurable heuristics (GUI or YAML) + parser and runtime.

## Later
- AI for "one project vs many" with bounded context.
- Remote backend + client sync.
- Full-text search over scan metadata.

## Done (recent)
- Ignore build/IDE artifacts in scan (`bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `*.pdb`, `*.obj`, `*.tlog`, `*.exe`, `*.suo`).
- IPC for scans (start/stop/pause/resume) + `ScanSession` in SQLite.
- Per-disk lock and whole-scan blocking.
- Scan snapshot to JSON with content samples.
- Pulsing progress bar for `Running` scan.
