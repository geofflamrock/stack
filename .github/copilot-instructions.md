# Copilot Instructions

Purpose: Enable AI agents to quickly understand and extend the `stack` CLI (branch & PR stack management). Keep answers grounded in this repo's concrete patterns.

## Architecture Snapshot

- **Entry**: `Program.cs` builds DI host then invokes `StackRootCommand` (System.CommandLine pattern).
- **Command Structure**: Each feature = command class inheriting `Infrastructure/Commands/Command.cs` (adds common options, logging, error handling, input prompts). Command handlers are split: command class parses & constructs handler (e.g. `NewStackCommand` + `NewStackCommandHandler`). Follow this separation religiously.
- **Core Domains**:
  1. **Config (`Config/`)**: Persistent model (`Stack`, `Branch`) with schema migration v1→v2 in `FileStackConfig`. Config path is `%AppData%/stack/config.json` (Windows). v1 = linear list, v2 = tree structure. Backward compatibility is critical—use existing mapping helpers. **Stack Repository Pattern**: Command handlers access stacks through `IStackRepository` (scoped service) instead of `IStackConfig` directly. Repository automatically filters stacks by current git remote URI—handlers don't need to know about remote filtering. See "Stack Repository Pattern" section below.
  2. **Git (`Git/`)**: Thin wrapper over `git` & `gh` CLIs via `ProcessHelpers`. Never re-implement git porcelain—compose existing commands. Conflict detection surfaces as `ConflictException`.
  3. **Stack orchestration (`Commands/Helpers/StackActions.cs` & `StackHelpers.cs`)**: Implements update strategies (merge vs rebase), batch operations, PR list maintenance, status tree rendering (Spectre.Console). Pull request status lookups are best-effort and may be skipped if the GitHub CLI is unavailable.
  4. **GitHub integration (`GitHubClient`, `SafeGitHubClient`, `CachingGitHubClient`)**: Uses `gh pr` CLI JSON output. `SafeGitHubClient` swallows failures (missing CLI, auth, network) for status lookups only (`GetPullRequest`) and logs a single warning, ensuring commands still succeed; create/edit/open operations still propagate errors. Extend by adding fields to the source gen context.
- **UI**: Spectre.Console for colors, trees, emoji. Keep output terse; respect `--verbose` for extra process command output only. All dynamic strings must use `Markup.Escape()`.

## Key Conventions

- **Command Options**: Always add shared flags using `CommonOptions` static properties. Never duplicate option instances; reuse `CommonOptions.Stack`, `CommonOptions.Verbose`, etc.
- **Command Execution**: All goes through `SetAction` in base `Command`; override `Execute` method or delegate to separate handler class returning `Task`.
- **Logging Levels**: `Information` for user-facing progress, `Debug` gated by `--verbose`, `Warning` for recoverable issues, `Error` only in base when exceptions bubble up.
- **User Prompts**: Centralized in `Questions.cs`; reuse constants instead of hard‑coding strings. Pattern: `await inputProvider.SelectFromEnum<T>(logger, question, cancellationToken)`.
- **Hierarchical Data**: Branch & PR status objects (`StackStatus`, `BranchDetail`) use tree structures. Use existing traversal helpers (`GetAllBranchLines`, `GetAllBranches`).
- **Update Strategy**: Respect explicit flags first (`--merge`/`--rebase`), then git config key `stack.update.strategy`, else prompt. Use `StackHelpers.GetUpdateStrategy`.
- **Conflict Handling**: Catch `ConflictException`, prompt using corresponding question from `Questions.cs`, call `Abort*`/`Continue*` methods. Mirror existing patterns in `StackActions`.
- **PR Body Management**: Use `StackConstants.StackMarkerStart/End` markers. Only mutate PR bodies within those markers to preserve user content.
- **JSON Serialization**: Uses source generators. Add new config schema types to `StackConfigJsonSerializerContext`.

## External Dependencies & Assumptions

- **Git Dependencies**: Relies on `git` and (optionally) `gh` CLI in PATH. Never embed credentials logic—inherit from git configuration.
- **Process Execution**: All external commands go through `ProcessHelpers.ExecuteProcessAndReturnOutput` with consistent error handling via `ProcessException`.
- **Test Infrastructure**: Tests simulate repos via `LibGit2Sharp` helpers (`TestGitRepositoryBuilder`). Prefer extending fluent builder over ad-hoc repo setup.
- **Markup Safety**: All dynamic strings in Spectre.Console output must use `Markup.Escape()` to prevent injection.
- **Directory Paths**: Always use absolute paths. Working directory handling is centralized through `CliExecutionContext.WorkingDirectory`.

## Adding a New Command (Example Flow)

1. **Create Command Class**: `Commands/<Area>/<Name>Command.cs` inheriting `Command` base class.
2. **Define Options**: Define any new `Option<T>` locally; reuse existing ones from `CommonOptions` static properties.
3. **Wire Dependencies**: In constructor, `Add(...)` options and set description; register in `StackRootCommand` constructor.
4. **Implement Execute**: Override `Execute` method. Inject `IStackRepository` (instead of `IStackConfig`), `IGitClientFactory`, and `CliExecutionContext` via handler constructor.
5. **Separate Handler Logic**: Put non-trivial business logic in separate handler class (suffix `CommandHandler`) inheriting `CommandHandlerBase<TInput>` to keep command class thin and testable.
6. **Exception Handling**: Follow existing patterns—let `ProcessException` bubble for standardized error output via base `Command` class.

## Testing Patterns

- **Structure**: Unit tests reside in mirrored folder structure under `Stack.Tests`.
- **Test Data**: Use `Some.*` helpers for random names instead of hardcoded strings (e.g., `Some.BranchName()`, `Some.Name()`).
- **Assertions**: Keep assertions focused; many tests use `FluentAssertions` with `AssertionScope` for multiple checks.
- **Mocking**: Prefer unit tests with substitutes using `NSubstitute` for testing command handlers.
- **Git Scenarios**: Build test repos using fluent `TestGitRepositoryBuilder` pattern (supports branch creation, commits, pushes, config values).

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
