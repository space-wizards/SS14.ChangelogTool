using System.Collections.Immutable;

namespace SS14.ChangelogTool.Models.GitHub;

public sealed record GitHubPushedCommit(ImmutableArray<string> Added, ImmutableArray<string> Modified);