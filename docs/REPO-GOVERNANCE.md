# Repository Governance and Release Policy

Last audited **2026-10-30**. This document records the repository settings confirmed after the maintainer completed the GitHub UI configuration.

Committed files (workflows, Dependabot config, CODEOWNERS, SECURITY.md) do not enforce repository-level settings by themselves; the GitHub-hosted controls below are configured separately.

## Branch model

- Repository visibility: **public**.
- Default and release branch: `main`. Integration branch: `dev`.
- Feature branches target `dev`; `dev` is merged to `main` via pull request.
- The active `Protect main` ruleset targets the default branch.

## Default-branch ruleset: Protect main

Confirmed through the GitHub rulesets API on 2026-10-30:

- Target: **Include default branch** (`~DEFAULT_BRANCH`).
- **Restrict deletions:** enabled.
- **Block force pushes:** enabled.
- **Require a pull request before merging:** enabled.
- Required approving reviews: **0**. This is intentional for a solo-maintained repository; GitHub does not count self-approval.
- **Require status checks to pass:** enabled, with these exact job/check names:
  - `Analyze C#` (CodeQL workflow).
  - `Restore and build (Windows)` (Windows CI workflow on `main`).
- **Require branches to be up to date:** disabled.
- Bypass actors: none.
- Allowed merge methods in the ruleset: squash and rebase.

The required checks have successful runs recorded on `main`. The workflow job names must remain aligned with the ruleset if the workflows are renamed.

## CI coverage by branch

The Windows CI workflow currently differs between `main` and `dev`:

- On `main`, `.github/workflows/ci.yml` defines the job `Restore and build (Windows)`. It runs `dotnet restore .\testExtension.sln` and `dotnet build .\testExtension.sln --configuration Release --no-restore`. It does **not** run `dotnet test`. Therefore, the required Windows check on `main` currently verifies restore/build only.
- On `dev`, the workflow defines `Restore, build, and test (Windows)` and delegates to `scripts/ci.ps1`. That script restores and builds the solution, then runs `dotnet test` against `PlayniteCmdPal.Core.Tests/PlayniteCmdPal.Core.Tests.csproj` in Release configuration.

The integration branch already has automated test execution. When the tested CI workflow is promoted from `dev` to `main`, update the `Protect main` required-check name to the exact resulting job name (if it changes) and verify successful runs on `main`. Until then, do not claim that the main-branch required Windows check runs tests.

## Security settings

| Setting | Confirmed state on 2026-10-30 | Notes |
| --- | --- | --- |
| Private vulnerability reporting | **Enabled** | Confirmed by GitHub API: `enabled: true`. |
| Dependabot alerts | **Enabled** | GitHub API returned HTTP 204 for the vulnerability-alerts endpoint. |
| Dependabot security updates | **Enabled** | Confirmed by the maintainer in repository settings. |
| CodeQL code scanning | **Working** | Successful scheduled, push-triggered, and pull-request runs have been observed. |
| Secret scanning / push protection | **Not verified** | Re-check under Settings → Advanced Security if needed. |

Dependabot version updates are configured in `.github/dependabot.yml`.

## Actions permissions

Confirmed through the GitHub Actions workflow-permissions API on 2026-10-30:

- Default workflow permissions: **Read repository contents and packages** (`read`).
- **Allow GitHub Actions to create and approve pull requests:** disabled.

The workflows also declare minimal top-level permissions, use `persist-credentials: false` for checkout, and configure timeouts/concurrency cancellation where applicable.

## Repository features and licensing

- Visibility: **public**.
- Discussions: **disabled**.
- Wiki: disabled. Issues and Projects: enabled.
- No license file is currently committed. Public visibility does not grant permission to reuse the code; choose and commit a license before any release.
- No GitHub Release or release tag has been created.
- Automatic deletion of merged head branches is not recorded here as verified.

## CI status badges

The README contains Windows CI and CodeQL badges. Successful runs on `main` were verified before adding them. The badge workflows are `.github/workflows/ci.yml` and `.github/workflows/codeql.yml`.

## Release policy

Do not create a GitHub Release or version tag until all of the following are complete:

1. A license file is committed and licensing of direct and transitive dependencies is reviewed.
2. A tagged build artifact passes CI and a documented manual smoke test covering local deployment and Playnite integration.
3. A release checklist exists, covering artifact contents, versioning, changelog, and signing decision.

Until then, the project is pre-release and the default branch is the current development baseline.

## Re-audit commands

Run these commands with an authenticated GitHub CLI session:

```powershell
gh api repos/werdscopez7347/PlayniteCmdPal/rulesets
gh api repos/werdscopez7347/PlayniteCmdPal/rulesets/26190984
gh api repos/werdscopez7347/PlayniteCmdPal/private-vulnerability-reporting
gh api repos/werdscopez7347/PlayniteCmdPal/vulnerability-alerts -i
gh api repos/werdscopez7347/PlayniteCmdPal/actions/permissions/workflow
gh workflow list --repo werdscopez7347/PlayniteCmdPal
gh run list --repo werdscopez7347/PlayniteCmdPal --branch main --limit 20
gh run list --repo werdscopez7347/PlayniteCmdPal --branch dev --limit 20
```
