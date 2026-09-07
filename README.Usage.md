# SS14 Changelog Tool — Configuration & Usage

There are following list of env variables that you can set before running the tool. 
Some of them are mandatory to start tool.

```
# *MANDATORY* repository to inspect for missed changes in PRs
REPO=space-wizards/space-station-14
# Sets the API the host for the Forgejo PR provider. Default: `codeberg.org`
HOST=github.com
# Sets the PR provider, this is what is used to query for PRs. `Forgejo` is another option. Default: `GitHub`
PR_PROVIDER=GitHub
# *MANDATORY* path to changelog files inside repo
CHANGELOG_REPO_PATH=Resources/Changelog
# The main file that will be updated when generating changelogs that are not targeting some special extra categories.
# Forks will typically want to change this value to match the name of their changelog file.
PRIMARY_CHANGELOG=Changelog
# comma-separated list of additional changelog categories; creates/updates separate YAML files like Admin.yml, Maps.yml, etc.
EXTRA_CATEGORIES=Admin,Maps,Rules
# *MANDATORY* GitHub personal access token or Actions workflow token with read access to pull requests
GITHUB_TOKEN=<github personal access token or workflow token>
# Discord webhook URL for the send-webhook command; if omitted, repsective command will throw
DISCORD_WEBHOOK=<discord webhook url>
# maximum number of characters per Discord message; longer diffs are split into multiple messages if possible
DISCORD_WEBHOOK_CHARACTER_LIMIT=2000
# maximum number of changelog entries to keep per file; oldest entries are pruned first
MAX_CHANGELOG_ENTRIES=500
# maximum number of pull requests to fetch in a single GraphQL request
MAX_PULL_REQUEST_ENTRIES_IN_GRAPHQL_REQUEST=50
# maximum retries number for gh api calls when they fail or requests have to consecutively waiting due to rate-limiting
MAX_RETRIES_FOR_GIT_HUB_API=12
# maximum wait time between attempts for gh api calls when call fails; uses exponential backoff retries, starts with Min_WAIT_FOR_GIT_HUB_API_SECONDS
MAX_WAIT_FOR_GIT_HUB_API_SECONDS=32
# minimum wait time between attempts for gh api calls when api call fails; uses exponential backoff retries
MIN_WAIT_FOR_GIT_HUB_API_SECONDS=2
# If set to true - when doing update, will check each commit and only add changelog from commits that were added in current GitHub repository (set by REPO config value), and discard others.
IS_PROCESS_ONLY_FROM_CURRENT_REPO_ENABLED = true
```

Recommended minimal local setup configuration
``` powershell
 $env:REPO="space-wizards/space-station-14"
 $env:CHANGELOG_REPO_PATH="Resources/Changelog"
 $env:EXTRA_CATEGORIES="Admin,Maps,Rules"
 $env:GITHUB_TOKEN="<your gh api key with READ permission for PRs>"
```

### Core commands
- Update changelogs

  Walks the local git history from the current branch tip back to the newest commit that touched
  the changelog files. Extracts PR numbers from commit messages (`(#NNN)` suffix), fetches the PR details from GitHub
  through the GraphQL API in batches, and appends the parsed changelog entries to the local YAML files.
  Revert commits (messages containing "revert", e.g. `Revert: 44644 - 40090 - 37716 - 42439 - 41004 (#44924)`) are
  recognized as well: the referenced PRs are removed from the incoming entries and their existing changelog entries
  are deleted from the YAML files.
- Use `-ac` / `--allow-create` argument to force creation of new changelog files for categories that don't exist yet. Otherwise, the tool will throw error upon detecting missing files.

  Example:
  ```powershell
  ss14-changelog -- update -d C:\path\to\repo\Resources\Changelog
  ```

  You also can use it with starting sha specified, then tool will not try to search for last changes in changelog files and simply will get all changes since specified sha value (if it can).
  Example:
  ```powershell
  ss14-changelog -- update -d C:\path\to\repo\Resources\Changelog -s da04a52e80246454ae577bfc9b56989064a96b60
  ```

- Dump diff

  Walks the local git history from the current branch tip back to the provided `--sha` commit, then extracts PR numbers from commit messages and fetches the PR details from GitHub through the GraphQL API. Writes a human-readable markdown diff of the parsed changelog entries, optionally excluding a category (`--except-category`).
  The `--sha` must exist in the local clone.

  Examples:
  ```powershell
  ss14-changelog -- dump-diff -s <ref-sha> -c diff.md
  ss14-changelog -- dump-diff -s <ref-sha> -c diff.md --except-category Admin
  ```

- Send webhook

  Reads a previously generated markdown diff from disk and posts it to the `DISCORD_WEBHOOK` URL. Long diffs are split into multiple messages according to `DISCORD_WEBHOOK_CHARACTER_LIMIT` (default 2000).

  Example:
  ```powershell
  ss14-changelog -- send-webhook -c diff.md
  ```
