using SS14.ChangelogTool.Models.GitHub;

namespace SS14.ChangelogTool.Services;

/// <summary>
/// Service for interacting with github api.
/// </summary>
public interface IGitHubPullRequestService
{
    /// <summary>
    /// Downloads changelog.yml file from GH content API by provided <see cref="refSha"/>
    /// and extracts changelog entry with newest pr merge time, then outputs it.
    /// Repo and branch are set by app settings.
    /// Checks other changelog files if <see cref="extraCategories"/> are passed.
    /// </summary>
    /// <param name="refSha">RefSha to be used for last merged change check.</param>
    /// <param name="extraCategories">Extra categories of changelogs to include. By default, only reads 'Changelog.yml'.</param>
    /// <returns></returns>
    DateTimeOffset GetNewestChangelogEntryMergeDateByRef(string refSha, IReadOnlyCollection<string> extraCategories);

    /// <summary>
    /// Gets list of pull-requests that have date of merge greater than provided one.
    /// Repo and branch are set by app settings.
    /// </summary>
    Task<IReadOnlyCollection<GitHubPullRequest>> GetDiff(DateTimeOffset olderThen);
}
