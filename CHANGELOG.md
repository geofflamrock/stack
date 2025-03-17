# v0.10.1 (Mon Mar 17 2025)

#### 🐛 Bug Fixes

- Fixes confirmation when deleting stack in non-interactive console [#236](https://github.com/geofflamrock/stack/pull/236) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.10.0 (Fri Mar 14 2025)

#### 🚀 New Features and Improvements

- Improve rebase after squash merge [#235](https://github.com/geofflamrock/stack/pull/235) ([@geofflamrock](https://github.com/geofflamrock))
- Add confirm option to `sync` command [#231](https://github.com/geofflamrock/stack/pull/231) ([@geofflamrock](https://github.com/geofflamrock))
- Add confirm option to `cleanup` command [#230](https://github.com/geofflamrock/stack/pull/230) ([@geofflamrock](https://github.com/geofflamrock))
- Add confirm option to `delete` command [#229](https://github.com/geofflamrock/stack/pull/229) ([@geofflamrock](https://github.com/geofflamrock))
- Add confirm option to `branch remove` command [#228](https://github.com/geofflamrock/stack/pull/228) ([@geofflamrock](https://github.com/geofflamrock))
- Removes confirmations from `stack branch new` command [#227](https://github.com/geofflamrock/stack/pull/227) ([@geofflamrock](https://github.com/geofflamrock))
- Removes confirmations from `stack new` command [#226](https://github.com/geofflamrock/stack/pull/226) ([@geofflamrock](https://github.com/geofflamrock))
- Adds `--json` output formatting [#225](https://github.com/geofflamrock/stack/pull/225) ([@geofflamrock](https://github.com/geofflamrock))
- Write output to `stdout` for `list` and `status` commands [#224](https://github.com/geofflamrock/stack/pull/224) ([@geofflamrock](https://github.com/geofflamrock))
- Change logging to use `stderr` [#223](https://github.com/geofflamrock/stack/pull/223) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.9.1 (Tue Feb 11 2025)

#### 🐛 Bug Fixes

- Improve error handling of push failures when creating new branches [#214](https://github.com/geofflamrock/stack/pull/214) ([@geofflamrock](https://github.com/geofflamrock))

#### 📝 Documentation

- Add table of contents to readme [#212](https://github.com/geofflamrock/stack/pull/212) ([@geofflamrock](https://github.com/geofflamrock))
- Improve introduction in readme [#211](https://github.com/geofflamrock/stack/pull/211) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.9.0 (Wed Feb 05 2025)

#### 🚀 New Features and Improvements

- Adds support for applying labels when creating pull requests [#210](https://github.com/geofflamrock/stack/pull/210) ([@geofflamrock](https://github.com/geofflamrock))
- Adds command to set pull request description [#204](https://github.com/geofflamrock/stack/pull/204) ([@geofflamrock](https://github.com/geofflamrock))
- Improve way that stack PR list is handled in pull request body [#203](https://github.com/geofflamrock/stack/pull/203) ([@geofflamrock](https://github.com/geofflamrock))
- Support selecting branches to create pull requests for [#202](https://github.com/geofflamrock/stack/pull/202) ([@geofflamrock](https://github.com/geofflamrock))
- Make options to select a stack consistent [#192](https://github.com/geofflamrock/stack/pull/192) ([@geofflamrock](https://github.com/geofflamrock))

#### 🐛 Bug Fixes

- Fix issue where wrong abort call is made during rebase [#209](https://github.com/geofflamrock/stack/pull/209) ([@geofflamrock](https://github.com/geofflamrock))
- Don't cleanup local branch that hasn't been pushed to remote [#201](https://github.com/geofflamrock/stack/pull/201) ([@geofflamrock](https://github.com/geofflamrock))
- Fixes examples in `stack status` command to show `--stack` option [#197](https://github.com/geofflamrock/stack/pull/197) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Ignore major version updates to FluentAssertions [#205](https://github.com/geofflamrock/stack/pull/205) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.8.1 (Fri Jan 10 2025)

#### 🐛 Bug Fixes

- Escape markup in status command [#189](https://github.com/geofflamrock/stack/pull/189) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.8.0 (Wed Jan 08 2025)

#### 🚀 New Features and Improvements

- Replace `--push` option with asking whether to push to remote [#186](https://github.com/geofflamrock/stack/pull/186) ([@geofflamrock](https://github.com/geofflamrock))
- Removes `--force` option [#185](https://github.com/geofflamrock/stack/pull/185) ([@geofflamrock](https://github.com/geofflamrock))
- Removes `--dry-run` option [#182](https://github.com/geofflamrock/stack/pull/182) ([@geofflamrock](https://github.com/geofflamrock))
- Add support for selecting merge or rebase using git config [#179](https://github.com/geofflamrock/stack/pull/179) ([@geofflamrock](https://github.com/geofflamrock))
- Adds support for rebase during update and sync [#178](https://github.com/geofflamrock/stack/pull/178) ([@geofflamrock](https://github.com/geofflamrock))
- Improves merge conflict handling [#177](https://github.com/geofflamrock/stack/pull/177) ([@geofflamrock](https://github.com/geofflamrock))

#### 🐛 Bug Fixes

- Fixes showing delete example when all branches can be cleaned up [#184](https://github.com/geofflamrock/stack/pull/184) ([@geofflamrock](https://github.com/geofflamrock))
- Don't show deleted branches in `switch` command [#180](https://github.com/geofflamrock/stack/pull/180) ([@geofflamrock](https://github.com/geofflamrock))
- Fixes issue with creating PR when no template exists in the repo [#175](https://github.com/geofflamrock/stack/pull/175) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.7.0 (Sun Jan 05 2025)

#### 🚀 New Features and Improvements

- Changes `stack update` command to operate on local branches [#167](https://github.com/geofflamrock/stack/pull/167) ([@geofflamrock](https://github.com/geofflamrock))
- Adds `stack sync` command [#166](https://github.com/geofflamrock/stack/pull/166) ([@geofflamrock](https://github.com/geofflamrock))
- Adds `--push` option when creating new branches [#165](https://github.com/geofflamrock/stack/pull/165) ([@geofflamrock](https://github.com/geofflamrock))
- Adds `stack push` command [#164](https://github.com/geofflamrock/stack/pull/164) ([@geofflamrock](https://github.com/geofflamrock))
- Adds `stack pull` command [#160](https://github.com/geofflamrock/stack/pull/160) ([@geofflamrock](https://github.com/geofflamrock))
- Show local status by default in `stack status` command [#159](https://github.com/geofflamrock/stack/pull/159) ([@geofflamrock](https://github.com/geofflamrock))

#### 🐛 Bug Fixes

- Fixes issue with pulling branch that doesn't exist locally [#174](https://github.com/geofflamrock/stack/pull/174) ([@geofflamrock](https://github.com/geofflamrock))
- Improve status of untracked branches [#170](https://github.com/geofflamrock/stack/pull/170) ([@geofflamrock](https://github.com/geofflamrock))

#### 🔩 Dependency Updates

- Bump coverlet.collector from 6.0.2 to 6.0.3 in /src [#169](https://github.com/geofflamrock/stack/pull/169) ([@dependabot[bot]](https://github.com/dependabot[bot]))

#### 🏠 Internal

- Rename `IGitHubOperations` to `IGitHubClient` [#172](https://github.com/geofflamrock/stack/pull/172) ([@geofflamrock](https://github.com/geofflamrock))
- Rename `IGitOperations` to `IGitClient` [#171](https://github.com/geofflamrock/stack/pull/171) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Improve release note category names [#173](https://github.com/geofflamrock/stack/pull/173) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 2

- [@dependabot[bot]](https://github.com/dependabot[bot])
- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.6.1 (Fri Dec 27 2024)

#### 🐛 Bug Fix

- Fixes issue with "double quotes" in PR [#161](https://github.com/geofflamrock/stack/pull/161) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.6.0 (Thu Dec 19 2024)

#### 🚀 New Feature

- Ask before editing the PR body [#138](https://github.com/geofflamrock/stack/pull/138) ([@geofflamrock](https://github.com/geofflamrock))

#### 🐛 Bug Fix

- Escape special characters from input default values [#135](https://github.com/geofflamrock/stack/pull/135) ([@geofflamrock](https://github.com/geofflamrock))

#### 🏠 Internal

- Use output provider for Git and GitHub operations [#141](https://github.com/geofflamrock/stack/pull/141) ([@geofflamrock](https://github.com/geofflamrock))

#### 🧪 Tests

- Use proper Git repository for open pull requests tests [#158](https://github.com/geofflamrock/stack/pull/158) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for create pull requests tests [#157](https://github.com/geofflamrock/stack/pull/157) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for update stack tests [#156](https://github.com/geofflamrock/stack/pull/156) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for stack status tests [#155](https://github.com/geofflamrock/stack/pull/155) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for stack switch tests [#154](https://github.com/geofflamrock/stack/pull/154) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for remove branch tests [#153](https://github.com/geofflamrock/stack/pull/153) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for new stack tests [#148](https://github.com/geofflamrock/stack/pull/148) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for delete stack tests [#147](https://github.com/geofflamrock/stack/pull/147) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for cleanup stack tests [#146](https://github.com/geofflamrock/stack/pull/146) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for new branch tests [#145](https://github.com/geofflamrock/stack/pull/145) ([@geofflamrock](https://github.com/geofflamrock))
- Use proper Git repository for add branch tests [#144](https://github.com/geofflamrock/stack/pull/144) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.5.0 (Wed Dec 11 2024)

#### 🚀 New Feature

- More improvements to pull request creation [#133](https://github.com/geofflamrock/stack/pull/133) ([@geofflamrock](https://github.com/geofflamrock))

#### 🔩 Dependency Updates

- Bump Octopus.Shellfish from 0.2.1180 to 0.2.2130 [#116](https://github.com/geofflamrock/stack/pull/116) ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump Microsoft.NET.Test.Sdk from 17.11.1 to 17.12.0 [#125](https://github.com/geofflamrock/stack/pull/125) ([@dependabot[bot]](https://github.com/dependabot[bot]))

#### Authors: 2

- [@dependabot[bot]](https://github.com/dependabot[bot])
- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.4.0 (Tue Dec 10 2024)

#### 🚀 New Feature

- Add support for creating pull requests as drafts [#131](https://github.com/geofflamrock/stack/pull/131) ([@geofflamrock](https://github.com/geofflamrock))
- Use pull request template if one exists [#130](https://github.com/geofflamrock/stack/pull/130) ([@geofflamrock](https://github.com/geofflamrock))
- Improve pull request creation [#123](https://github.com/geofflamrock/stack/pull/123) ([@geofflamrock](https://github.com/geofflamrock))

#### 🐛 Bug Fix

- Fixes issue where `status --all` would return stacks from wrong repository [#126](https://github.com/geofflamrock/stack/pull/126) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.3.2 (Fri Dec 06 2024)

#### 🐛 Bug Fix

- Fix issue with wrong target branch when creating PRs [#119](https://github.com/geofflamrock/stack/pull/119) ([@geofflamrock](https://github.com/geofflamrock))
- Removes styling of input [#107](https://github.com/geofflamrock/stack/pull/107) ([@geofflamrock](https://github.com/geofflamrock))

#### 🔩 Dependency Updates

- Bump FluentAssertions from 6.12.2 to 7.0.0 [#108](https://github.com/geofflamrock/stack/pull/108) ([@dependabot[bot]](https://github.com/dependabot[bot]))

#### 🤖 Automation

- Change to only use src directory for dependabot updates [#114](https://github.com/geofflamrock/stack/pull/114) ([@geofflamrock](https://github.com/geofflamrock))
- Fix directories for nuget updates [#106](https://github.com/geofflamrock/stack/pull/106) ([@geofflamrock](https://github.com/geofflamrock))
- Adds dependabot for automated version updates [#105](https://github.com/geofflamrock/stack/pull/105) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 2

- [@dependabot[bot]](https://github.com/dependabot[bot])
- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.3.1 (Wed Dec 04 2024)

#### 🐛 Bug Fix

- Fixes issue with duplicate changelog entries in built artifacts [#93](https://github.com/geofflamrock/stack/pull/93) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Use GitHub App as committer for versioning PR [#102](https://github.com/geofflamrock/stack/pull/102) ([@geofflamrock](https://github.com/geofflamrock))
- Pass correct version number between workflow jobs [#101](https://github.com/geofflamrock/stack/pull/101) ([@geofflamrock](https://github.com/geofflamrock))
- Create versioning PR in separate workflow [#99](https://github.com/geofflamrock/stack/pull/99) ([@geofflamrock](https://github.com/geofflamrock))
- Add PR check jobs [#94](https://github.com/geofflamrock/stack/pull/94) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.3.0 (Tue Dec 03 2024)

#### 🚀 New Feature

- Suggest a default name when creating a branch [#84](https://github.com/geofflamrock/stack/pull/84) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Makes versioning PR job run after publish [#87](https://github.com/geofflamrock/stack/pull/87) ([@geofflamrock](https://github.com/geofflamrock))
- Put versioning PR back in again [#86](https://github.com/geofflamrock/stack/pull/86) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.2.2 (Mon Dec 02 2024)

#### 🐛 Bug Fix

- Fixes issue with double stack name output [#85](https://github.com/geofflamrock/stack/pull/85) ([@geofflamrock](https://github.com/geofflamrock))
- Don't include branch in update when PR is merged [#83](https://github.com/geofflamrock/stack/pull/83) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.2.1 (Sun Dec 01 2024)

#### 🐛 Bug Fix

- Improves input selection [#82](https://github.com/geofflamrock/stack/pull/82) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.2.0 (Thu Nov 28 2024)

#### 🚀 New Feature

- Automatically select stack if there is only one for a repository [#81](https://github.com/geofflamrock/stack/pull/81) ([@geofflamrock](https://github.com/geofflamrock))

#### 🏠 Internal

- Refactors to add tests for `pr open` command [#80](https://github.com/geofflamrock/stack/pull/80) ([@geofflamrock](https://github.com/geofflamrock))
- Refactors to add tests for `pr create` command [#79](https://github.com/geofflamrock/stack/pull/79) ([@geofflamrock](https://github.com/geofflamrock))
- Refactors to add tests for branch commands [#78](https://github.com/geofflamrock/stack/pull/78) ([@geofflamrock](https://github.com/geofflamrock))
- Remove specific input providers [#77](https://github.com/geofflamrock/stack/pull/77) ([@geofflamrock](https://github.com/geofflamrock))

#### 📝 Documentation

- Adds initial readme [#76](https://github.com/geofflamrock/stack/pull/76) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.1.1 (Sun Nov 24 2024)

#### 🐛 Bug Fix

- Fix issue where `config` command would not run [#75](https://github.com/geofflamrock/stack/pull/75) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Update changelog before creating artifact [#70](https://github.com/geofflamrock/stack/pull/70) ([@geofflamrock](https://github.com/geofflamrock))
- Exclude docs changes from build workflow [#72](https://github.com/geofflamrock/stack/pull/72) ([@geofflamrock](https://github.com/geofflamrock))
- Add changelog and readme to artifacts [#69](https://github.com/geofflamrock/stack/pull/69) ([@geofflamrock](https://github.com/geofflamrock))
- Fix changelog for initial automation [#68](https://github.com/geofflamrock/stack/pull/68) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))

---

# v0.1.0 (Sun Nov 24 2024)

#### 🚀 New Feature

- Adds `branch remove` command [#67](https://github.com/geofflamrock/stack/pull/67) ([@geofflamrock](https://github.com/geofflamrock))

#### 🤖 Automation

- Add workflow for creating versioning pr [#64](https://github.com/geofflamrock/stack/pull/64) ([@geofflamrock](https://github.com/geofflamrock))
- Remove versioning PR [#66](https://github.com/geofflamrock/stack/pull/66) ([@geofflamrock](https://github.com/geofflamrock))

#### Authors: 1

- Geoff Lamrock ([@geofflamrock](https://github.com/geofflamrock))
