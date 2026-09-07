using SS14.ChangelogTool.Models.Generic;

namespace SS14.ChangelogTool.Models.GitHub;

public sealed class GitHubPullRequestsResponse
{
    public Dictionary<string, GenericPullRequest?> Repository { get; set; } = [];
}