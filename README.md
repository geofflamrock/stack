# stack <!-- omit from toc -->

A tool to help manage multiple Git branches and pull requests that build on top of each other.

When you are working on a change to your software, breaking down and shipping your work in smaller, iterative chunks using separate branches and pull requests that build on each other brings a number of benefits:

- It's easier to understand and reason about a smaller set of changes.
- Getting someone to review your changes is easier if they are smaller and well contained. You are also more likely to get a high quality review that could catch issues compared to a massive pull request with a large number of changes which will often get skimmed.
- Integration into long-lived branches such as `main` is easier and less prone to unintended issues.
- It's easier to revert or fix if there is an issue.
- Iterating on a change promotes getting feedback early and adjusting if you need to as you understand and complete the work.

This approach is not without it's tradeoffs though:

- It can be hard to get the overall context or scope of a change across multiple branches and pull requests.
- Keeping branches up to date with the latest changes in `main` is difficult, time-consuming and can easily be done incorrectly, leading to needing to untangle a mess of conflicts.
- Getting reviews from other team members or other teams can take time.
- Incorporating feedback from reviews across branches is difficult.

This is where `stack` comes in: It lets you manage multiple branches that form together into a stack, along with their associated pull requests, helping you to overcome the tradeoffs and gain the benefits of small, iterative changes.

**Note: This project is under active development and is likely to have rough edges, bugs and missing things. Until it reaches `v1.0.0` it also might change at any time.**

## Contents <!-- omit from toc -->

- [Getting started](#getting-started)
- [How does it work?](#how-does-it-work)
  - [Creating a stack](#creating-a-stack)
  - [Working within a stack](#working-within-a-stack)
  - [Adding a new branch](#adding-a-new-branch)
  - [Incorporating changes from the remote repository](#incorporating-changes-from-the-remote-repository)
    - [Specifying an update strategy](#specifying-an-update-strategy)
  - [Creating pull requests](#creating-pull-requests)
- [Commands](#commands)

## Getting started

### Prerequisites <!-- omit from toc -->

In order to use `stack` you'll need the following:

- The `git` CLI installed, added to your path and configured to access your repository.
- (optional) The `gh` CLI installed, added to your path and authenticated if you want to use some of the GitHub integration points.

### Installing `stack` <!-- omit from toc -->

To install stack download the relevant binary for your OS from the [latest GitHub release](https://github.com/geofflamrock/stack/releases), unarchive it and (optionally) add `stack` to your path.

Run `stack` to get a list of the available commands.

## How does it work?

Multiple branches are managed in a **stack**. This is an _explicit_ set of branches that you want to manage and keep updated as you work. `stack` doesn't try and automatically work out which branches were first created from other ones, it takes the opinion that you are in control of which branches you to manage together. Every stack has a **source branch** which it starts from, this is likely to be the default branch of your repository.

`stack` operates using the `git` and (optionally) `gh` CLI to perform the branch actions that you likely would do if you were trying to manage branches yourself. As a result it doesn't need to store any specific credentials, and inherits any defaults you might have for your Git configuration.

All commands can be run from anywhere inside the Git repository you are working with, or optionally using the `--working-dir` option.

Data is stored inside a config file in `{user}/stack/config.json`. You can open the config file by running `stack config open`.

### Creating a stack

To create a stack:

- In your terminal, change to your Git repository.
- Run `stack new`.
- Give your stack a name.
- Select a branch to start your stack from.
- Optionally either create a new branch from the source branch, or add an existing branch to the stack.

If a new branch was not able to be pushed to the remote, you can use the `stack push` command to push the branch to the remote later.

### Working within a stack

Working within a stack is the same as working with Git as per normal, make your changes on the branch, commit them and push them to the remote. You likely have your own tooling and workflows for this, you can continue to use them.

### Adding a new branch

Once you've done some work on the first branch within the stack, at some point you'll likely want a second branch. To do this:

- Run `stack branch new`.
- Select the stack to create the branch in.
- Give the branch a name.
- Select the branch to create the new branch from.

The new branch will be created from the selected parent branch and the current branch will be changed so you can make more changes.

If a new branch was not able to be pushed to the remote, you can use the `stack push` command to push the branch to the remote later.

### Incorporating changes from the remote repository

After working on a stack of branches for a while, you might need to incorporate changes that have happened to your source branch from others. To do this:

- Run `stack sync`
- Select the stack you wish to sync
- Confirm the sync

Branches in the stack will be updated by:

- Fetching changes to the repository, pruning remote branches that no longer exist, the equivalent of running `git fetch --prune`.
- Pulling changes for all branches in the stack, including the source branch, the equivalent of running `stack pull`.
- Updating branches in order in the stack, the equivalent of running `stack update`.
- Pushing changes for all branches in the stack, the equivalent of running `stack push`.

#### Specifying an update strategy

There are two strategies that can be used to update branches in a stack.

The Git configuration key `stack.update.strategy` can be used to control the default update strategy on a global or per-repository basis.

You will be asked to select an update strategy if none is supplied or configured.

##### Merge <!-- omit from toc -->

When using the merge update strategy, each branch in the stack is merged into the one directly below it, starting with the source branch.

To use the merge strategy, either:

- Supply the `--merge` option to the `sync` or `update` command.
- Configure `stack.update.strategy` to be `merge` in Git configuration using `git config stack.update.strategy merge`.

**Rough edges**

Updating a stack using merge, particularly if it has a number of branches in it, can result in lots of merge commits.

If you merge a pull request using "Squash and merge" then you might find that the first update to a stack after that results in merge conflicts that you need to resolve. This can be a bit of a pain.

##### Rebase <!-- omit from toc -->

When using the rebase update strategy, each branch in the stack is rebased on it's parent branch.

To use the rebase strategy, either:

- Supply the `--rebase` option to the `sync` or `update` command.
- Configure `stack.update.strategy` to be `rebase` in Git configuration using `git config stack.update.strategy rebase`.

To push changes to the remote after rebasing you'll need to use the `--force-with-lease` option.

**_Squash merges_**

A common pattern when using pull requests is to Squash Merge the pull request when merging into the target branch, squashing all the commits in the PR branch into a single commit. This causes issues when rebasing the rest of the child branches in the stack.

Stack has handling to detect when a squash merge happens during updating a stack using rebase as the update strategy. It will skip the commits that were squash merged, avoiding conflicts.

The remote tracking branch for the branch that was squash merged needs to be deleted for this handling to be enabled.

### Creating pull requests

When you've made your changes you can create a set of pull requests that build off each other. This requires that you have the `gh` CLI installed on your path and authenticated (run `gh auth login`).

To do this:

- Run `stack pr create`.
- Confirm that you want to create pull requests for the stack.
- For each branch you'll be asked for the title of the pull request.
- The pull request will then be created, targeting the previous branch in the stack.

When all the pull requests have been created set of links to all pull requests in the stack will be put into the body of each pull request. This is optional and is controlled by the presence of the `<!-- stack-pr-list -->` comment in the body of the pull request. To opt-out just remove the comment.

You can then open each pull request if the stack if you want to view them.

`stack pr create` can be run multiple times, if there are new branches in the stack that don't have an associated pull request these will be created and the list updated on each pull request.

## Commands

### Stack commands <!-- omit from toc -->

#### `stack new` <!-- omit from toc -->

Create a new stack.

```shell
Usage:
  stack new [options]

Options:
  --working-dir        The path to the directory containing the git repository. Defaults to the current directory.
  --debug              Show debug output.
  --verbose            Show verbose output.
  --json               Write output and log messages as JSON. Log messages will be written to stderr.
  -n, --name           The name of the stack. Must be unique within the repository.
  -s, --source-branch  The source branch to use for the new stack. Defaults to the default branch for the repository.
  -b, --branch         The name of the branch to create within the stack.
  -?, -h, --help       Show help and usage information
```

#### `stack list` <!-- omit from toc -->

List stacks.

```shell
Usage:
  stack list [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -?, -h, --help  Show help and usage information
```

#### `stack status` <!-- omit from toc -->

Show the status of the current stack or all stacks in the repository.

```shell
Usage:
  stack status [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  --all           Show status of all stacks.
  --full          Show full status including pull requests.
  -?, -h, --help  Show help and usage information
```

#### `stack delete` <!-- omit from toc -->

Delete a stack.

```shell
Usage:
  stack delete [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -y, --yes       Confirm the command without prompting.
  -?, -h, --help  Show help and usage information
```

#### `stack rename` <!-- omit from toc -->

Rename a stack.

```shell
Usage:
  stack rename [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -n, --name      The new name for the stack.
  -?, -h, --help  Show help and usage information
```

### Branch commands <!-- omit from toc -->

#### `stack update` <!-- omit from toc -->

Update the branches in a stack.

```shell
Usage:
  stack update [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  --rebase        Use rebase when updating the stack. Overrides any setting in Git configuration.
  --merge         Use merge when updating the stack. Overrides any setting in Git configuration.
  -?, -h, --help  Show help and usage information
```

#### `stack switch` <!-- omit from toc -->

Switch to a branch in a stack.

```shell
Usage:
  stack switch [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -b, --branch    The name of the branch.
  -?, -h, --help  Show help and usage information
```

#### `stack cleanup` <!-- omit from toc -->

Clean up branches in a stack that are no longer needed.

```shell
Usage:
  stack cleanup [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -y, --yes       Confirm the command without prompting.
  -?, -h, --help  Show help and usage information
```

#### `stack branch new` <!-- omit from toc -->

Create a new branch in a stack.

```shell
Usage:
  stack branch new [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -b, --branch    The name of the branch.
  -p, --parent    The name of the parent branch to put the branch under.
  -?, -h, --help  Show help and usage information
```

#### `stack branch add` <!-- omit from toc -->

Add an existing branch to a stack.

```shell
Usage:
  stack branch add [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -b, --branch    The name of the branch.
  -p, --parent    The name of the parent branch to put the branch under.
  -?, -h, --help  Show help and usage information
```

#### `stack branch remove` <!-- omit from toc -->

Remove a branch from a stack.

```shell
Usage:
  stack branch remove [options]

Options:
  --working-dir              The path to the directory containing the git repository. Defaults to the current directory.
  --debug                    Show debug output.
  --verbose                  Show verbose output.
  --json                     Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack                The name of the stack.
  -b, --branch               The name of the branch.
  -y, --yes                  Confirm the command without prompting.
  --remove-children          Remove children branches.
  --move-children-to-parent  Move children branches to the parent branch.
  -?, -h, --help             Show help and usage information
```

#### `stack branch move` <!-- omit from toc -->

Move a branch to another location in a stack.

```shell
Usage:
  stack branch move [options]

Options:
  --working-dir         The path to the directory containing the git repository. Defaults to the current directory.
  --debug               Show debug output.
  --verbose             Show verbose output.
  --json                Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack           The name of the stack.
  -b, --branch          The name of the branch.
  -p, --parent          The name of the parent branch to put the branch under.
  --re-parent-children  Re-parent child branches to the current parent of the branch being moved.
  --move-children       Move child branches with the branch being moved.
  -?, -h, --help        Show help and usage information
```

### Remote commands <!-- omit from toc -->

#### `stack pull` <!-- omit from toc -->

Pull changes from the remote repository for a stack.

```shell
Usage:
  stack pull [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -?, -h, --help  Show help and usage information
```

#### `stack push` <!-- omit from toc -->

Push changes to the remote repository for a stack.

```shell
Usage:
  stack push [options]

Options:
  --working-dir       The path to the directory containing the git repository. Defaults to the current directory.
  --debug             Show debug output.
  --verbose           Show verbose output.
  --json              Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack         The name of the stack.
  --max-batch-size    The maximum number of branches to process at once. [default: 5]
  --force-with-lease  Force push changes with lease.
  -?, -h, --help      Show help and usage information
```

#### `stack sync` <!-- omit from toc -->

Sync a stack with the remote repository. Shortcut for `git fetch --prune`, `stack pull`, `stack update` and `stack push`.

```shell
Usage:
  stack sync [options]

Options:
  --working-dir     The path to the directory containing the git repository. Defaults to the current directory.
  --debug           Show debug output.
  --verbose         Show verbose output.
  --json            Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack       The name of the stack.
  --max-batch-size  The maximum number of branches to process at once. [default: 5]
  --rebase          Use rebase when updating the stack. Overrides any setting in Git configuration.
  --merge           Use merge when updating the stack. Overrides any setting in Git configuration.
  -y, --yes         Confirm the command without prompting.
  --no-push         Don't push changes to the remote repository
  -?, -h, --help    Show help and usage information
```

### GitHub commands <!-- omit from toc -->

#### `stack pr create` <!-- omit from toc -->

Create pull requests for a stack.

```shell
Usage:
  stack pr create [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -?, -h, --help  Show help and usage information
```

#### `stack pr open` <!-- omit from toc -->

Open pull requests for a stack in the default browser.

```shell
Usage:
  stack pr open [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -s, --stack     The name of the stack.
  -?, -h, --help  Show help and usage information
```

### Advanced commands <!-- omit from toc -->

#### `stack config open` <!-- omit from toc -->

Open the configuration file in the default editor.

```shell
Usage:
  stack config open [options]

Options:
  --working-dir   The path to the directory containing the git repository. Defaults to the current directory.
  --debug         Show debug output.
  --verbose       Show verbose output.
  --json          Write output and log messages as JSON. Log messages will be written to stderr.
  -?, -h, --help  Show help and usage information
```
