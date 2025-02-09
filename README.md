# stack

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

## Getting started

### Prerequisites

In order to use `stack` you'll need the following:

- The `git` CLI installed, added to your path and configured to access your repository.
- (optional) The `gh` CLI installed, added to your path and authenticated if you want to use some of the GitHub integration points.

### Installing `stack`

To install stack download the relevant binary for your OS from the [latest GitHub release](https://github.com/geofflamrock/stack/releases), unarchive it and (optionally) add `stack` to your path.

Run `stack` to get a list of the available commands.

## How does it work?

Multiple branches are managed in a **stack**. This is an _explicit_ set of branches that you want to manage and keep updated as you work. `stack` doesn't try and automatically work out which branches were first created from other ones, it takes the opinion that you are in control of which branches you to manage together. Every stack has a **source branch** which it starts from, this is likely to be the default branch of your repository.

`stack` operates using the `git` and (optionally) `gh` CLI to perform the branch actions that you likely would do if you were trying to manage branches yourself. As a result it doesn't need to store any specific credentials, and inherits any defaults you might have for your Git configuration.

All commands can be run from anywhere inside the Git repository you are working with, or optionally using the `--working-dir` option.

Data is stored inside a config file in `{user}/stack/config.json`. You can open the config file by running `stack config`.

### Creating a stack

To create a stack:

- In your terminal, change to your Git repository.
- Run `stack new`.
- Give your stack a name.
- Select a branch to start your stack from.
- Optionally either create a new branch from the source branch, or add an existing branch to the stack.
- Optionally push a new branch to the remote repository.
- If you chose to create or add a branch you can switch to that branch to start work.

If a new branch was not pushed to the remote, you can use the `stack push` command to push the branch to the remote.

### Working within a stack

Working within a stack is the same as working with Git as per normal, make your changes on the branch, commit them and push them to the remote. You likely have your own tooling and workflows for this, you can continue to use them.

### Adding a new branch to the stack

Once you've done some work on the first branch within the stack, at some point you'll likely want a second branch. To do this:

- Run `stack branch new`.
- Select the stack to create the branch in.
- Give the branch a name.
- Optionally push a new branch to the remote repository.

The new branch will be created from the branch at the bottom of the stack and you can then switch to the branch if you would like to in order to make more changes.

If a new branch was not pushed to the remote, you can use the `stack push` command to push the branch to the remote.

### Syncing a stack with the remote repository

After working on a stack of branches for a while, you might need to incorporate changes that have happened to your source branch from others. To do this:

- Run `stack sync`
- Select the stack you wish to sync
- Confirm the sync

Branches in the stack will be updated by:

- Fetching changes to the repository, pruning remote branches that no longer exist, the equivalent of running `git fetch --prune`.
- Pulling changes for all branches in the stack, including the source branch, the equivalent of running `stack pull`.
- Updating branches in order in the stack, the equivalent of running `stack update`.
- Pushing changes for all branches in the stack, the equivalent of running `stack push`.

### Update strategies

There are two strategies that can be used to update branches in a stack.

The Git configuration key `stack.update.strategy` can be used to control the default update strategy on a global or per-repository basis.

The `merge` update strategy is used by default if no configuration is supplied.

#### Merge

When using the merge update strategy, each branch in the stack is merged into the one directly below it, starting with the source branch.

To use the merge strategy, either:

- Supply the `--merge` option to the `sync` or `update` command.
- Configure `stack.update.strategy` to be `merge` in Git configuration using `git config stack.update.strategy merge`.

**Rough edges**

Updating a stack using merge, particularly if it has a number of branches in it, can result in lots of merge commits.

If you merge a pull request using "Squash and merge" then you might find that the first update to a stack after that results in merge conflicts that you need to resolve. This can be a bit of a pain.

#### Rebase

When using the rebase update strategy, each branch in the stack is rebased on it's parent branch.

To use the rebase strategy, either:

- Supply the `--rebase` option to the `sync` or `update` command.
- Configure `stack.update.strategy` to be `rebase` in Git configuration using `git config stack.update.strategy rebase`.

To push changes to the remote after rebasing you'll need to use the `--force-with-lease` option.

**Rough edges**

If you merge a pull request using "Squash and merge" then you might find that the first update to a stack after that results in merge conflicts that you need to resolve. This can be a bit of a pain, however for each commit that existed on the branch that was merged if you select to take the new single commit that now exists generally it isn't too bad.

### Creating pull requests for the stack

When you've made your changes you can create a set of pull requests that build off each other. This requires that you have the `gh` CLI installed on your path and authenticated (run `gh auth login`).

To do this:

- Run `stack pr create`.
- Confirm that you want to create pull requests for the stack.
- For each branch you'll be asked for the title of the pull request.
- The pull request will then be created, targeting the previous branch in the stack.

When all the pull requests have been created you'll be asked for a pull request stack description if there is more than 1 pull request in the stack. This will be added to the top of the description of each pull request along with a set of links to all pull requests in the stack. For an example of this look at https://github.com/geofflamrock/stack/pull/32.

You can then open each pull request if the stack if you want to view them.

`stack pr create` can be run multiple times, if there are new branches in the stack that don't have an associated pull request these will be created and the description updated on each pull request.

## Stack commands

### `stack new`

Creates a new stack.

```shell
USAGE:
    stack new [OPTIONS]

OPTIONS:
    -h, --help             Prints help information
    -v, --version          Prints version information
        --verbose          Show verbose output
        --working-dir      The path to the directory containing the git repository. Defaults to the current directory
    -n, --name             The name of the stack. Must be unique
        --source-branch    The source branch to use for the new branch. Defaults to the default branch for the repository
    -b, --branch           The name of the branch to create within the stack
```

### `stack list`

Lists stacks for the current repository.

```shell
USAGE:
    stack list [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
```

### `stack status`

Shows the status of a stack, including commits compared to other branches and optionally the status of any associated pull requests.

```shell
USAGE:
    stack status [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to show the status of
        --all            Show status of all stacks
        --full           Show full status including pull requests
```

### `stack delete`

Deletes a stack. If there are local branches which no longer exist on the remote or the associated pull request is no longer open these can be deleted as part of the command.

```shell
USAGE:
    stack delete [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to delete
```

## Branch commands

### `stack update`

Updates the branches for a stack by either merging or rebasing each branch.

```shell
USAGE:
    stack update [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to update
        --rebase         Use rebase when updating the stack. Overrides any setting in Git configuration
        --merge          Use merge when updating the stack. Overrides any setting in Git configuration
```

### `stack switch`

Switches to a different branch in the current stack or another stack.

```shell
USAGE:
    stack switch [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -b, --branch         The name of the branch to switch to
```

### `stack cleanup`

Cleans up local branches in a stack which no longer exist on the remote or where the associated pull request is no longer open.

```shell
USAGE:
    stack cleanup [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to cleanup
```

### `stack branch new`

Creates a new branch from the last branch in the stack and adds it.

```shell
USAGE:
    stack branch new [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to create the branch in
    -n, --name           The name of the branch to create
```

### `stack branch add`

Adds an existing branch to the end of the stack.

```shell
USAGE:
    stack branch add [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to create the branch in
    -n, --name           The name of the branch to add
```

### `stack branch remove`

Removes a branch from a stack.

```shell
USAGE:
    stack branch remove [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to create the branch in
    -n, --name           The name of the branch to add
```

## Remote commands

### `stack pull`

Pulls changes from the remote repository for a stack.

```shell

USAGE:
    stack pull [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to pull changes from the remote for
```

### `stack push`

Pushes changes to the remote repository for a stack.

```shell

USAGE:
    stack push [OPTIONS]

OPTIONS:
    -h, --help                Prints help information
    -v, --version             Prints version information
        --verbose             Show verbose output
        --working-dir         The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack               The name of the stack to push changes from the remote for
        --max-batch-size      The maximum number of branches to push changes for at once (default: 5)
        --force-with-lease    Force push changes with lease
```

### `stack sync`

Syncs a stack with the remote repository. Shortcut for `git fetch --prune`, `stack pull`, `stack update` and `stack push`.

```shell

USAGE:
    stack sync [OPTIONS]

OPTIONS:
    -h, --help              Prints help information
    -v, --version           Prints version information
        --verbose           Show verbose output
        --working-dir       The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack             The name of the stack to sync with the remote
        --max-batch-size    The maximum number of branches to push changes for at once (default: 5)
        --rebase            Use rebase when updating the stack. Overrides any setting in Git configuration
        --merge             Use merge when updating the stack. Overrides any setting in Git configuration
```

## GitHub commands

### `stack pr create`

Creates and/or updates pull requests for each branch in a stack.

```shell
USAGE:
    stack pr create [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to create pull requests for
```

### `stack pr open`

Opens pull requests for a stack in the default browser.

```shell
USAGE:
    stack pr open [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to open PRs for
```

### `stack pr description`

Sets the pull request description for the stack and applies it all pull requests.

```shell
USAGE:
    stack pr description [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
    -s, --stack          The name of the stack to open PRs for
```

## Advanced commands

### `stack config`

Opens the configuration file in the default editor.

```shell
USAGE:
    stack config [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
```
