# SS14 Changelog Tool

Small CLI tool to generate changelogs for SS14 repositories.

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
- Commands are registered in DI as `UpdateCommand`, `DumpDiffCommand`, and `SendWebhookCommand`.
- File operations are abstracted behind `IFileManager` and the code uses 

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
