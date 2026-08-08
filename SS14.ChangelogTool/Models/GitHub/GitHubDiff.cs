namespace SS14.ChangelogTool.Models.GitHub;

/// <summary>
/// Result of a diff collected from local git history.
/// </summary>
/// <param name="PullRequests">Pull requests whose changelog entries should be added/updated.</param>
/// <param name="RevertedPullRequestNumbers">
/// PR numbers of pull requests that were reverted in the range and whose changelog entries should be removed.
/// </param>
public sealed record GitHubDiff(
    IReadOnlyCollection<GitHubPullRequest> PullRequests,
    IReadOnlyCollection<int> RevertedPullRequestNumbers
);