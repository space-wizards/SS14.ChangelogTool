using SS14.ChangelogTool.Models.GitHub;

namespace SS14.ChangelogTool.Services;

/// <summary>
/// Service for interacting with github api.
/// </summary>
public interface IGitHubPullRequestService
{
    /// <summary>
    /// Get date of last merged change since marked ref in repository.
    /// </summary>
    /// <param name="sinceRefSha">RefSha to be used for last merged change check.</param>
    /// <param name="extraCategories">Extra categories of changelogs to include. By default, only reads 'Changelog.yml'.</param>
    /// <returns></returns>
    DateTimeOffset GetLastMergedFromRef(string sinceRefSha, IReadOnlyCollection<string> extraCategories);

    /// <summary>
    /// Gets list of pull-requests that have date of merge greater than provided one.
    /// Repo and branch are set by app settings.
    /// </summary>
    Task<IReadOnlyCollection<GitHubPullRequest>> GetDiff(DateTimeOffset olderThen);
}
