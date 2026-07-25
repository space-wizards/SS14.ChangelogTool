using SS14.ChangelogTool.Models.GitHub;

namespace SS14.ChangelogTool.Clients;

/// <summary>
/// Wrapper for extracting GitHub pull request info through GraphQL API.
/// </summary>
public interface IGithubPullRequestClient
{
    /// <summary>
    /// Extracts pull requests that have merge date greater, then provided date.
    /// </summary>
    /// <param name="repo">Repo to inspect.</param>
    /// <param name="branch">Branch to inspect.</param>
    /// <param name="olderThen">Cutoff for merge date.</param>
    Task<IReadOnlyCollection<GitHubPullRequest>> GetPullRequestsOlderThen(string repo, string branch, DateTimeOffset olderThen);
}