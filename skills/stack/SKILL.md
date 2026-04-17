---
name: stack
description: "Use the stack CLI to manage stacked branches and pull requests. Use when: creating a new stack, adding or removing branches from a stack, moving branches within a stack, syncing a stack with the remote repository, checking stack status, or managing stacked pull requests."
argument-hint: What would you like to do with your stack?
---

# Stack CLI

A tool for managing multiple Git branches and pull requests that build on top of each other (stacked branches/PRs).

## Prerequisites Check

Before running any stack commands, verify the CLI is available:

```shell
stack --version
```

If the command fails or is not found, prompt the user:

> The `stack` CLI is not installed or not in your PATH.
> Download the latest release for your OS from: https://github.com/geofflamrock/stack/releases
> Unarchive the binary and add it to your PATH, then try again.

Also check that `git` is available (required) and optionally `gh` (for GitHub/PR features):

```shell
git --version
gh --version
```

## When to Use

- Creating a new stack of branches
- Adding a new branch to an existing stack
- Adding an existing branch to a stack
- Removing or moving a branch within a stack
- Syncing a stack with remote (fetch + pull + update + push)
- Checking the status of a stack
- Updating stack branches without pushing

---

## Procedures

### Create a New Stack

```shell
stack new [--name <name>] [--source-branch <branch>] [--branch <branch>]
```

**Interactive flow** (no flags needed — stack will prompt):

1. Run `stack new`
2. Enter a unique name for the stack
3. Select a source branch (usually `main` or `master`)
4. Choose to: create a new branch, add an existing branch, or skip

**Non-interactive example:**

```shell
stack new --name my-feature --source-branch main --branch feature/first-step
```

After creation, the stack is saved in `{user}/stack/config.json`.

---

### Manage Branches in a Stack

#### Create a new branch in a stack

```shell
stack branch new [--stack <stack>] [--branch <name>] [--parent <parent-branch>]
```

- Creates a new branch from the specified parent (or prompts)
- Pushes the branch to remote
- Switches to the new branch

#### Add an existing local branch to a stack

```shell
stack branch add [--stack <stack>] [--branch <name>] [--parent <parent-branch>]
```

- Branch must already exist locally
- Prompts for which stack and parent branch if not specified

#### Remove a branch from a stack

```shell
stack branch remove [--stack <stack>] [--branch <name>] [--move-children-to-parent | --remove-children] [-y]
```

- If the branch has children, you must choose to move them to the parent or remove them
- Use `-y` to skip the confirmation prompt

#### Move a branch to a new position in a stack

```shell
stack branch move [--stack <stack>] [--branch <name>] [--parent <new-parent>] [--move-children | --re-parent-children]
```

- After moving, run `stack sync` or `stack update` to synchronize git history

---

### Sync a Stack

`stack sync` is the most common way to keep a stack up to date. It runs fetch, pull, update, and push in sequence.

```shell
stack sync [--stack <stack>] [--rebase | --merge] [-y] [--no-push] [--check-pull-requests]
```

**Options:**
| Flag | Description |
|------|-------------|
| `--rebase` | Rebase each branch onto its parent |
| `--merge` | Merge each parent into child branch |
| `--no-push` | Skip pushing changes to remote |
| `-y` | Skip confirmation prompt |
| `--check-pull-requests` | Skip branches whose PR is already merged |

**Default update strategy:** Checks `stack.update.strategy` in git config, or prompts if not set.

**Configure a default strategy:**

```shell
git config stack.update.strategy rebase   # or: merge
```

**Conflict handling:** If conflicts occur during sync, resolve them and continue. The command will surface any `ConflictException` and prompt for next steps.

---

### Update Stack Branches (without push)

```shell
stack update [--stack <stack>] [--rebase | --merge] [--check-pull-requests]
```

Updates branches in order within the stack without fetching from or pushing to the remote. Useful when you only need to rebase/merge local branches.

---

### Check Stack Status

```shell
stack status [--stack <stack>] [--all] [--check-pull-requests]
```

Shows a tree view of the stack including:

- Each branch and whether it exists locally/remotely
- Commits ahead/behind the parent branch
- PR status (if `--check-pull-requests` is used and `gh` CLI is available)

---

### Other Useful Commands

| Command           | Description                                                      |
| ----------------- | ---------------------------------------------------------------- |
| `stack list`      | List all stacks for the current repository                       |
| `stack switch`    | Switch to a branch in a stack                                    |
| `stack cleanup`   | Remove branches that are no longer needed (merged/squash-merged) |
| `stack rename`    | Rename a stack                                                   |
| `stack delete`    | Delete a stack (does not delete the git branches)                |
| `stack pr create` | Create stacked pull requests via `gh` CLI                        |

---

## Tips

- All commands can be run from anywhere inside the git repository
- Use `--working-dir <path>` to target a specific repo from any directory
- Use `--verbose` to see the underlying git commands being run
- Use `--json` for structured output suitable for scripting
- Stack config is stored at `{user}/stack/config.json` — open with `stack config open`
