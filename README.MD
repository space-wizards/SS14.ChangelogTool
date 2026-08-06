# SS14 Changelog Tool

Small CLI tool to generate changelogs for SS14 repositories.

Configuration
Set the following environment variables (or bind them in your environment) before running:

```
# repository to inspect for missed changes in PRs
REPO=space-wizards/space-station-14
# name of branch to inspect for missed changes in PRs
BRANCH=master
# path to changelog files inside repo
CHANGELOG_REPO_PATH=Resources/Changelog
# primary outputted changelog yaml file; default outputs Changelog.yml
PRIMARY_CHANGELOG=Changelog
# comma-separated list of additional changelog categories; creates/updates separate YAML files like Admin.yml, Maps.yml, etc.
EXTRA_CATEGORIES=Admin,Maps,Rules
# GitHub personal access token or Actions workflow token with read access to pull requests
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
Min_WAIT_FOR_GIT_HUB_API_SECONDS=2
```

### Core commands
- Update changelogs

  Updates the changelog YAML files by finding PRs merged since the last changelog entry and appending parsed changes.

  Example:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- update -d C:\path\to\Resources\Changelog
  ```

- Dump diff

  Produces a human-readable markdown file containing changes since a ref SHA.
  Optionally exclude a category (e.g. `Admin`) from the output with `--except-category`.

  Examples:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- dump-diff -s <ref-sha> -c diff.md
  dotnet run --project SS14.ChangelogTool -- dump-diff -s <ref-sha> -c diff.md --except-category Admin
  ```

- Send webhook

  Sends a previously-generated markdown diff to the configured Discord webhook. The message is split by the configured
  `DISCORD_WEBHOOK_CHARACTER_LIMIT` (default 2000).

  Example:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- send-webhook -c diff.md
  ```

### Architecture notes
- Commands are registered in DI as `UpdateCommand`, `DumpDiffCommand`, and `SendWebhookCommand`.
- File operations are abstracted behind `IFileManager` and the code uses 
- Options are bound to `ChangelogConfigOptions` and validated on startup via `ValidateOnStart()` and a custom validator.
- Use `System.IO.Abstractions` where possible for better testability


### Testing
- Unit tests live in `SS14.ChangelogTool.Tests`. Use XUnit, NSubstitute, if needed - can check manipulations by using interfaces that are provided by System.IO.Abstractions. Run tests with:

```powershell
dotnet test
```

### Building

```powershell
dotnet build
```


### CI/CD workflows

This repository has two GitHub Actions workflows:

- **Build and Test** (`.github/workflows/build.yml`) — runs on every pull request to `master`.  
  Restores dependencies, builds in Release mode, and runs all tests.

- **Build and Publish** (`.github/workflows/publish.yml`) — runs when a tag matching `v*` is pushed.  
  Builds, tests, packs the project into a NuGet package, and pushes it to `https://api.nuget.org/v3/index.json`  
  using the `NUGET_API_KEY` secret.

  The project is configured as a **.NET tool** with the command name `ss14-changelog`.  
  After publishing, anyone can install and run it.

### HOW TO USE

Tool installation:
```powershell
dotnet tool install --global SS14.ChangelogTool
ss14-changelog --help
```

To update to the latest version:
```powershell
dotnet tool update --global SS14.ChangelogTool
```

Using tool: 

```powershell
ss14-changelog update -d C:\path\to\Resources\Changelog
# commit updated YMLs, build, run publish tests
```

- Creating a diff for a stable publish:

```powershell
ss14-changelog -- dump-diff -s <last-publish-ref> -c diff.md
ss14-changelog -- send-webhook -c diff.md
```
