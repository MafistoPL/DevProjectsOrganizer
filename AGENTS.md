# Instructions for Codex

- Before implementing any request, provide a brief plan of intended changes (components to add/remove/move, files to touch).
- Ask for confirmation only when the plan is complex (multi-step, architectural, or touching many files).
- For simple actions (e.g., run tests, small styling tweaks), proceed without asking.
- If changing tests to match implementation behavior (instead of fixing implementation), ask for explicit user confirmation first.
- For IPC/API payload changes (FE <-> AppHost), add/update contract tests on both sides:
  - FE service test must assert exact request payload shape.
  - AppHost test must assert payload parser/handler accepts the intended minimal payload and rejects invalid input.
- Keep `PLAN.md` up to date as the spec evolves.
- Keep `BACKLOG.md` up to date with deferred tasks and follow-ups.
- If you execute only part of a user request, add the unimplemented remainder to `BACKLOG.md`.
- Suggest good moments to commit when the changes form a coherent milestone.
- If you want a PNG in `PLAN.md`, create/update the source `docs/excalidraw/*.excalidraw` and run `scripts/export-excalidraw.ps1` to generate PNGs in `docs/images`.
