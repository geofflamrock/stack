# Copilot Instructions

Purpose: Enable AI agents to quickly understand and extend the `stack` CLI (branch & PR stack management). Keep answers grounded in this repo's concrete patterns.

## Architecture Snapshot

- Entry: `Program.cs` wires a `StackRootCommand` (System.CommandLine) and invokes.
- Each feature = a command class inheriting custom abstract `Commands.Command` (adds common options, logging, error handling, input prompts).
- Command handlers are split: command class parses & constructs handler (e.g. `NewStackCommand` + `NewStackCommandHandler`). Follow this separation when adding commands.
- Core domains:
  1. Config (`Config/`): Persistent model (`Stack`, `Branch`, schema migration v1→v2 in `FileStackConfig`). Config path is `%AppData%/stack/config.json` (Windows) unless overridden. v1 = linear list, v2 = tree. Preserve backward compatibility when touching serialization; use existing mapping helpers.
  2. Git (`Git/`): Thin wrapper over `git` & `gh` CLIs via `ProcessHelpers`. Never re-implement porcelain logic—compose Git commands and rely on existing exception patterns. Conflict detection surfaces as `ConflictException`.
  3. Stack orchestration (`Commands/Helpers/StackActions.cs` & `StackHelpers.cs`): Implements update strategies (merge vs rebase), PR list maintenance, status tree rendering (Spectre.Console).
  4. GitHub integration (`GitHubClient`, `CachingGitHubClient`): Uses `gh pr` JSON. Extend by adding fields to the source gen context.
- Rendering & interaction: Spectre.Console (color, trees, emoji). Keep output terse; respect `--verbose` for extra process command output only.

## Key Conventions

- Always add shared flags using `CommonOptions`. Do not duplicate option instances; reuse the static properties.
- All command execution goes through `SetAction` set in base `Command`; wrap logic in `Execute` override or separate handler class returning `Task`.
- Logging levels: `Information` for user-facing progress, `Debug` gated by `--verbose`, `Warning` for recoverable issues, `Error` only in base when exceptions bubble.
- User prompts centralised in `Questions.cs`; reuse constants instead of hard‑coding strings.
- Branch & PR status objects (`StackStatus`, `BranchDetail`, etc.) are hierarchical. Use existing traversal helpers (`GetAllBranchLines`, `GetAllBranches`).
- Update strategy selection: respect explicit flags first (`--merge`/`--rebase`), then git config key `stack.update.strategy`, else prompt. Use `StackHelpers.GetUpdateStrategy`.
- Conflict handling: Catch `ConflictException`, prompt using corresponding question, call Abort*/Continue* methods. Mirror existing patterns (see `StackActions` and `StackHelpers`).
- PR stack section markers: use `StackConstants.StackMarkerStart/End`. Only mutate PR bodies within those markers.
- JSON serialization uses source generators. If adding new config schema types, extend `StackConfigJsonSerializerContext`.

## External Dependencies & Assumptions

- Relies on `git` and (optionally) `gh` in PATH. Do not embed credentials logic.
- Tests simulate repos via `LibGit2Sharp` helpers (`TestGitRepositoryBuilder`). Prefer extending builder over ad-hoc repo setup.
- Color / markup must be escaped with `Markup.Escape` for dynamic strings.

## Adding a New Command (Example Flow)

1. Create `Commands/<Area>/<Name>Command.cs` inheriting `Command`.
2. Define any new `Option<T>` locally; reuse existing ones from `CommonOptions`.
3. In ctor, `Add(...)` the options and set description; add to `StackRootCommand` constructor.
4. Implement `Execute` that instantiates required clients: `new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory))`, config via `FileStackConfig()`, and (if GitHub) `CachingGitHubClient`.
5. Put non-trivial business logic in a handler class (suffix `CommandHandler`) to keep the command thin and testable.
6. Follow existing exception handling (let `ProcessException` bubble for standardized error output).

## Testing Patterns

- Unit tests reside in mirrored folder structure under `Stack.Tests`.
- Use `Some.*` helpers for random names. Use this instead of hardcoded strings.
- Keep assertions focused (many tests use `AssertionScope`).
- Preference unit tests with substitutes using `NSubstitute` for testing command handlers.
- For git scenarios: build repos with fluent `TestGitRepositoryBuilder` (branch creation, commits, pushes, config values).

## Safe Change Guidelines

- Before altering update logic, scan both `StackActions` and `StackHelpers`—there are duplicated algorithm variants (instance vs static). Keep parity or consolidate consciously.
- Maintain backward compatibility for config v1: never write multi-tree stacks while still in v1; guard with existing `HasSingleTree` checks.
- When adding data to PR body, only modify within marker block; avoid overwriting user content elsewhere.
- Keep CLI output stable (used by users & possibly scripts). Add new info behind flags if noisy.

## Common Gotchas

- Rebase logic depends on ancestor detection (`git merge-base --is-ancestor`). If adjusting rebase sequencing, preserve handling of inactive (deleted/squash merged) branches.
- `PushBranches` batches by `--max-batch-size`; maintain grouping semantics if altering.
- Always re-check current branch after stack update to restore user's prior branch where needed.

## Typical Dev Workflow

- Build: `dotnet build` at solution root.
- Run CLI: `dotnet run --project src/Stack -- <args>`.
- Run tests: `dotnet test` (fast; heavy git operations stubbed by test builder).
- Verbose debugging: append `--verbose` to any stack command.

## When Unsure

Prefer composing existing helpers (StackHelpers / StackActions) over new ad-hoc logic. Reference `NewStackCommand` and `UpdateStackCommand` as templates.

---

Refine this file if new patterns emerge (especially schema changes or new update strategies).
