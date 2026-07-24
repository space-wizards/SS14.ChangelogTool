using System.Collections.Immutable;

namespace SS14.ChangelogTool.Models.GitHub;

public sealed record GitHubPushEvent(ImmutableArray<GitHubPushedCommit> Commits, string Ref);