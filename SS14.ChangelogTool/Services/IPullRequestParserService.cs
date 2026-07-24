using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Models.GitHub;

namespace SS14.ChangelogTool.Services;

/// <summary>
/// Service for parsing pull request bodies and extracting changelog entries.
/// </summary>
public interface IPullRequestParserService
{
    /// <summary>
    /// Parse PR bodies and extract relevant changelog data as entries grouped by changelog category they are related.
    /// </summary>
    Dictionary<string, List<ChangelogEntry>> ExtractChangelogEntries(
        IEnumerable<GitHubPullRequest> pullRequests,
        List<string>? extraCategories = null
    );
}