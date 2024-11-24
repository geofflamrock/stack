# stack

A tool to help manage multiple Git branches and pull requests that stack on top of each other.

## What is this thing?

When working on a large new feature for your application, you might want to break the work for this into small chunks in separate branches so that it's easier to get each part reviewed by your team in a pull request and merged in. Each part of this work will likely need to build on the last part.

Managing multiple branches that all work together into a set of work can be a difficult task, particularly over time if you need to wait for reviews whilst your `main` branch has moved forward, or you need to incorporate feedback to your work.

This tool can help you to manage multiple branches that form together into a `stack`.

See [stacking.dev](https://www.stacking.dev/) for a longer description of the problem this tool is aiming to help you solve.

Note: This repo is under active development and is likely to have rough edges, bugs and missing things.

## Getting started

### Prerequisites

In order to use `stack` you'll need the following:

- The `git` CLI installed and added to your path
- The `gh` CLI if you want to use some of the GitHub integration points.

### Installing `stack`

To install stack download the relevant binary for your OS from the latest GitHub release, unarchive it and (optionally) add `stack` to your path.

## How does it work?

Multiple branches are managed in a **stack**. This is an _explicit_ set of branches that you want to manage and keep updated as you work. `stack` doesn't try and automatically work out which branches were first created from other ones, it takes the opinion that you are in control of which branches you to manage together. Every stack has a **source branch** which it starts from, this is likely to be the default branch of your repository.

`stack` operates using the `git` and (optionally) `gh` CLI to perform the branch actions that you likely would do if you were trying to manage branches yourself.

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

### Creating pull requests for the stack

When you've made your changes you can create a set of pull requests that build off each other. This requires that you have the `gh` CLI installed on your path and authenticated (`gh auth`).

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

### `stack list`

### `stack status`

### `stack delete`

## Branch commands

### `stack update`

### `stack switch`

### `stack cleanup`

### `stack branch new`

### `stack branch add`

### `stack branch remove`

## GitHub commands

### `stack pr create`

### `stack pr open`

## Advanced commands

### `stack config`
