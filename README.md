# stack

A tool to help manage multiple Git branches and pull requests that stack on top of each other.

## What is this thing?

When working on a large new feature for your application, it can be helpful to break your work into small chunks in separate branches. This makes it easier to get each part reviewed by your team in a pull request and merged in. Each part of this work will likely need to build on the last part.

Managing multiple branches and pull requests that all build together into a set of work can be a difficult task, particularly over time whilst you need to wait for reviews and your `main` branch has moved forward, or you need to incorporate feedback to your work.

This tool can help you to manage multiple branches that form together into a `stack`.

See [stacking.dev](https://www.stacking.dev/) for a longer description of the problem this tool is aiming to help you solve.

**Note: This project is under active development and is likely to have rough edges, bugs and missing things. Until it reaches `v1.0.0` it also might change at any time.**

## Getting started

### Prerequisites

In order to use `stack` you'll need the following:

- The `git` CLI installed, added to your path and configured to access your repository.
- The `gh` CLI installed, added to your path and authenticated if you want to use some of the GitHub integration points.

### Installing `stack`

To install stack download the relevant binary for your OS from the latest GitHub release, unarchive it and (optionally) add `stack` to your path.

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
- If you chose to create or add a branch you can switch to that branch to start work.

### Working within a stack

Working within a stack is the same as working with Git as per normal, make your changes on the branch, commit them and push them to the remote. You likely have your own tooling and workflows for this, you can continue to use them.

### Adding a new branch to the stack

Once you've done some work on the first branch within the stack, at some point you'll likely want a second branch. To do this:

- Run `stack branch new`.
- Select the stack to create the branch in.
- Give the branch a name.

The new branch will be created from the branch at the bottom of the stack and you can then switch to the branch if you would like to in order to make more changes.

### Updating a stack

After working on a stack of branches for a while, you might need to incorporate changes that have happened to your source branch from others. To do this:

- Run `stack update`
- Select the stack you wish to update
- Confirm the update

Branches in the stack will be updated by:

- Fetching the latest changes from the remote for all branches in the stack, including the source branch.
- Merging from the source branch to the first branch in the stack.
- Pushing changes for the first branch to the remote.
- Merging from the first branch to the second branch in the stack (if one exists).
- Pushing changes for the second branch to the remote.
- Repeating this until all branches are updated.

#### Rough edges

Updating a stack, particularly if it has a number of branches in it, can result in lots of merge commits. I'm exploring whether there are any improvements that can be made here for merging. I'd also like to support updating via a rebase as well in the future.

If you merge a pull request using "Squash and merge" then you might find that the first update to a stack after that results in merge conflicts that you need to resolve. This is a bit of a pain, I'm exploring whether there are any improvements that can be made here, perhaps by first merging into the just-merged local branch instead of ignoring it.

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
    -s, --source-branch    The source branch to use for the new branch. Defaults to the default branch for the repository
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
    -n, --name           The name of the stack to show the status of
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
    -n, --name           The name of the stack to delete
    -f, --force          Force cleanup and delete the stack without prompting
```

## Branch commands

### `stack update`

Updates the branches for a stack by merging each branch.

```shell
USAGE:
    stack update [OPTIONS]

OPTIONS:
    -h, --help           Prints help information
    -v, --version        Prints version information
        --verbose        Show verbose output
        --working-dir    The path to the directory containing the git repository. Defaults to the current directory
        --dry-run        Show what would happen without making any changes
    -n, --name           The name of the stack to update
    -f, --force          Force the update of the stack
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
        --dry-run        Show what would happen without making any changes
    -n, --name           The name of the stack to cleanup
    -f, --force          Cleanup the stack without prompting
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
        --dry-run        Show what would happen without making any changes
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
        --dry-run        Show what would happen without making any changes
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
        --dry-run        Show what would happen without making any changes
    -s, --stack          The name of the stack to create the branch in
    -n, --name           The name of the branch to add
    -f, --force          Force removing the branch without prompting
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
        --dry-run        Show what would happen without making any changes
    -n, --name           The name of the stack to create pull requests for
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
        --dry-run        Show what would happen without making any changes
    -n, --name           The name of the stack to open PRs for
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
