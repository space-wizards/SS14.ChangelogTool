using SS14.ChangelogTool.Models.Generic;

namespace SS14.ChangelogTool.Clients;

/// <summary>
/// Wrapper for extracting GitHub data through GraphQL API.
/// </summary>
public interface INetworkGitRepositoryClient
{
    /// <summary>
    /// Extracts pull requests that have merge date greater, then provided date.
    /// </summary>
    /// <param name="repo">Repo to inspect, includes both repository name and owner, as '{owner}\{repo}'.</param>
    /// <param name="pullRequestNumbers">List of pull request numbers that we should retrieve.</param>
    Task<IReadOnlyCollection<GenericPullRequest>> GetPullRequests(string repo, IReadOnlyCollection<int> pullRequestNumbers);

    /// <summary>
    /// Returns the set of sha which introduced by the specified <paramref name="repo"/>.
    /// A commit is introduced by a repository if that repository contains a merged
    /// pull request whose merge commit is exactly this SHA and whose number
    /// matches the one referenced by the commit message.
    /// </summary>
    /// <param name="shaAndPrNumber">List of SHA and expected pull request number pairs to check.</param>
    /// <param name="repo">Repository to check against, in format of 'owner/repo'.</param>
    /// <returns>SHAs that were introduced by the specified repository.</returns>
    Task<IReadOnlyCollection<string>> GetCommitsIntroducedByRepo(
        IReadOnlyCollection<(string Sha, int PullRequestNumber)> shaAndPrNumber,
        string repo
    );
}
