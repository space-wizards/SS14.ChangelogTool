# SS14 Changelog Tool — Configuration & Usage

There are following list of env variables that you can set before running the tool. 
Some of them are mandatory to start tool.

```
# *MANDATORY* repository to inspect for missed changes in PRs
REPO=space-wizards/space-station-14
# *MANDATORY* name of branch to inspect for missed changes in PRs
BRANCH=master
# *MANDATORY* path to changelog files inside repo
CHANGELOG_REPO_PATH=Resources/Changelog
# comma-separated list of additional changelog categories; creates/updates separate YAML files like Admin.yml, Maps.yml, etc.
EXTRA_CATEGORIES=Admin,Maps,Rules
# *MANDATORY* GitHub personal access token or Actions workflow token with read access to pull requests and content
GITHUB_TOKEN=<github personal access token or workflow token>
# Discord webhook URL for the send-webhook command; if omitted, repsective command will throw
DISCORD_WEBHOOK=<discord webhook url>
# maximum number of characters per Discord message; longer diffs are split into multiple messages if possible
DISCORD_WEBHOOK_CHARACTER_LIMIT=2000
# maximum number of changelog entries to keep per file; oldest entries are pruned first
MAX_CHANGELOG_ENTRIES=500
# maximum number of GraphQL pages to traverse; raise this if you haven't updated the changelog in months
MAX_GRAPQHL_PAGES=50
# maximum retries number for gh api calls when they fail or requests have to consecutively waiting due to rate-limiting
MAX_RETRIES_FOR_GIT_HUB_API=12
# maximum wait time between attempts for gh api calls when call fails; uses exponential backoff retries, starts with Min_WAIT_FOR_GIT_HUB_API_SECONDS
MAX_WAIT_FOR_GIT_HUB_API_SECONDS=32
# minimum wait time between attempts for gh api calls when api call fails; uses exponential backoff retries
MIN_WAIT_FOR_GIT_HUB_API_SECONDS=2
```

Recommended minimal local setup configuration
``` powershell
 $env:REPO="space-wizards/space-station-14"
 $env:CHANGELOG_REPO_PATH="Resources/Changelog"
 $env:BRANCH="master"
 $env:EXTRA_CATEGORIES="Admin,Maps,Rules"
 $env:GITHUB_TOKEN="<your gh api key with READ permission for content and PRs>"
```

### Core commands
- Update changelogs

  Updates the changelog YAML files by finding PRs merged since the last changelog entry and appending parsed changes.

  Example:
  ```powershell
  ss14-changelog -- update -d C:\path\to\Resources\Changelog
  ```

- Dump diff

  Produces a human-readable markdown file containing changes since a ref SHA.
  Optionally exclude a category (e.g. `Admin`) from the output with `--except-category`.

  Examples:
  ```powershell
  ss14-changelog -- dump-diff -s <ref-sha> -c diff.md
  ss14-changelog -- dump-diff -s <ref-sha> -c diff.md --except-category Admin
  ```

- Send webhook

  Sends a previously-generated markdown diff to the configured Discord webhook. The message is split by the configured
  `DISCORD_WEBHOOK_CHARACTER_LIMIT` (default 2000).

  Example:
  ```powershell
  ss14-changelog -- send-webhook -c diff.md
  ```
