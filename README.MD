# SS14 Changelog Tool

Small CLI tool to generate changelogs for SS14 repositories. This project was inspired by the original
SS14 changelog tool and has been refactored for DI, testability, and resilience.

Key points
- Uses Microsoft.Extensions.DependencyInjection and the options pattern for configuration.
- Commands are registered in DI as `UpdateCommand`, `DumpDiffCommand`, and `SendWebhookCommand`.
- File operations are abstracted behind `IFileManager` and the code uses `System.IO.Abstractions` for testability.
- Pull request parsing is split into a parser (`IPullRequestParser` / `PullRequestParser`) and a GitHub API service (`IGitHubPullRequestService` / `GitHubPullRequestService`).
- Discord webhook sending uses a typed `HttpClient` with a Polly retry policy that treats 429 as transient.
- Regexes used to parse PR bodies are source-generated via `[GeneratedRegex]` for performance.

Configuration
Set the following environment variables (or bind them in your environment) before running:

```
REPO=space-wizards/space-station-14
BRANCH=master
CHANGELOG_REPO_PATH=Resources/Changelog
EXTRA_CATEGORIES=Admin,Maps,Rules
GITHUB_TOKEN=<github personal access token or workflow token>
DISCORD_WEBHOOK=<discord webhook url>
DISCORD_WEBHOOK_CHARACTER_LIMIT=2000
MAX_CHANGELOG_ENTRIES=500
```

Notes on configuration
- Options are bound to `ChangelogConfigOptions` and validated on startup via `ValidateOnStart()` and a custom validator.

Core commands
- Update changelogs

  Updates the changelog YAML files by finding PRs merged since the last changelog entry and appending parsed changes.

  Example:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- update -d C:\path\to\Resources\Changelog
  ```

- Dump diff

  Produces a human-readable markdown file containing changes since a ref SHA.

  Example:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- dump-diff -s <ref-sha> -c diff.md
  ```

- Send webhook

  Sends a previously-generated markdown diff to the configured Discord webhook. The message is split by the configured
  `DISCORD_WEBHOOK_CHARACTER_LIMIT` (default 2000).

  Example:
  ```powershell
  dotnet run --project SS14.ChangelogTool -- send-webhook -c diff.md
  ```

Architecture notes
- `IFileManager` / `FileManager` - encapsulates YAML read/write and changelog manipulation. Uses `System.IO.Abstractions` so tests
  can provide a `MockFileSystem` where appropriate. Some integration tests currently use the real file system due to MockFileSystem
  limitations with concurrent handles.
- `IPullRequestParser` / `PullRequestParser` - extracts changelog entries from PR bodies and reads existing changelog YAML to determine
  last-merged times for categories.
- `IGitHubPullRequestService` / `GitHubPullRequestService` - GraphQL calls to GitHub to fetch PRs and (optionally) download raw changelog
  files at a specific ref.
- `PullRequestHelper` - thin facade that composes the parser and GitHub service for consumers that previously depended on a single helper.

Testing
- Unit tests live in `SS14.ChangelogTool.Tests`. Run them with:

```powershell
dotnet test
```

- Tests use `System.IO.Abstractions.TestingHelpers` where possible. Some end-to-end tests run against the real file system by design.

Building

```powershell
dotnet build
```

Development workflow
- Typical publish testing:

```powershell
dotnet run --project SS14.ChangelogTool -- update -d C:\path\to\Resources\Changelog
# commit updated YMLs, build, run publish tests
```

- Creating a diff for a stable publish:

```powershell
dotnet run --project SS14.ChangelogTool -- dump-diff -s <last-publish-ref> -c diff.md
dotnet run --project SS14.ChangelogTool -- send-webhook -c diff.md
```

If you'd like, I can also add a short example workflow file for GitHub Actions that demonstrates using `dump-diff` and `send-webhook`.