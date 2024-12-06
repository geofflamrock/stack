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
