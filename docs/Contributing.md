# Contributing

Thank you for contributing to the Autodesk MCP Platform.

## Code of conduct

Be respectful and constructive. This project is a collaboration across many teams
and disciplines.

## Getting started

1. Fork the repository and clone it.
2. Follow `docs/DeveloperGuide.md` to install prerequisites and build.
3. Create a branch: `git checkout -b feat/your-change`.
4. Make the change, add tests, run the quality gate:
   ```bash
   npm run quality
   ```

## What to change

- **New Civil 3D tool** -> one C# class in the relevant `src/bridges/Civil3D.Tools.*`
  project + unit tests (see `docs/TOOL-DEVELOPMENT.md`). The server picks it up
  automatically - no server change, no manual registration.
- **Protocol / wire change** -> update both `Autodesk.Mcp.Shared` (C#) and
  `src/server/.../src/protocol` (TypeScript mirror) plus the round-trip tests on
  both sides, and bump the protocol version (breaking change = major bump).
- **Server behaviour** -> `src/server/Autodesk.Mcp.Server`, add/adjust Vitest tests.
- **Packaging / release** -> `eng/`, `packaging/`, `.github/workflows/`.
- **Docs** -> `docs/`, `examples/`.

## Definition of done

- Code compiles with **zero warnings** (warnings are errors in `src/`).
- New behaviour has tests; existing tests still pass.
- Server changes pass `typecheck`, `lint` and `test`.
- Documentation/examples updated when user-facing behaviour changes.
- `npm run quality` passes locally before pushing.

## Commits

- Small, focused commits with clear messages.
- Prefix with the area when it helps: `server:`, `bridge:`, `packaging:`, `docs:`.
- No generated `bin/`/`obj/`/`dist/`/`node_modules/` artifacts in commits.

## Pull requests

- One logical change per PR.
- Describe the change, the testing performed, and any compatibility notes.
- The CI quality gate must be green.
- Reviewers: keep review cycles fast; leave actionable comments.

## Releases

Only maintainers cut releases. See `docs/ReleaseProcess.md`.

## Reporting issues

Include: platform version, Civil 3D version, bridge/server versions (run
`autodesk-mcp-server --version`), the tool call, and relevant log excerpts
(redacted). See `docs/Troubleshooting.md`.
