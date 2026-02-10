# Backlog

## Next
- Add scan ignore rules for common build/IDE artifacts (`bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `*.pdb`, `*.obj`, `*.tlog`, `*.exe`, `*.suo`).
- Add project detection heuristics (markers like `.sln`, `.csproj`, `package.json`, `CMakeLists.txt`, `Makefile`, `pom.xml`, `build.gradle`, `.git`).
- Show root-level badges on Scan: project count and ongoing suggestions count.
- Expose a small scan summary model for each root in the UI (files scanned, last scan time, last state).

## Soon
- Pipeline from scan JSON to project suggestions list.
- Tag suggestion generation based on scan results (heuristics first).
- UI for queued scans and queue reason on Scan page.
- Optional manual split/merge of detected projects within a root.

## Later
- AI-assisted disambiguation: “one project vs many” with bounded context and size limits.
- Remote sync backend and client-side sync service.
- Full-text search over indexed scan metadata.
