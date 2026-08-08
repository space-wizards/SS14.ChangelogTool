using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Options;

namespace SS14.ChangelogTool.Services;

public delegate void WriteChangelog(
    Dictionary<string, List<ChangelogEntry>> changelogParts,
    IReadOnlyCollection<int> prNumbersToRevert
);

public class ChangelogGeneratorService(
    IPullRequestParserService parserService,
    IGitHubPullRequestService githubService,
    IOptions<ChangelogToolOptions> options,
    ILogger<ChangelogGeneratorService> logger
)
{
    private readonly ChangelogToolOptions _options = options.Value;

    /// <summary>
    /// Generates new changelog files by collecting records
    /// based on commit hash, provided by <see cref="lastChangeShaProvider"/> 
    /// and writes data using <see cref="changelogWriter"/>.
    /// </summary>
    public async Task<bool> TryGenerate(
        Func<IReadOnlyCollection<string>, string> lastChangeShaProvider,
        WriteChangelog changelogWriter
    )
    {
        List<string> extraCategories = [];
        if (_options.ExtraCategories is not null)
            extraCategories.AddRange(_options.ExtraCategories.Split(','));

        // Get the last merged PR time
        var lastMergeSha = lastChangeShaProvider(extraCategories);

        logger.LogInformation("Generating diff of commits since {LastMergedSha} til current state of local repository.", lastMergeSha);

        // Get the list of PRs that were merged since last time.
        var diff = await githubService.GetDiff(lastMergeSha);

        logger.LogInformation(
            "Collected {PullRequestCount} pull requests and {RevertedPullRequestCount} reverted pull requests.",
            diff.PullRequests.Count,
            diff.RevertedPullRequestNumbers.Count
        );

        // Generate a new YMLfest out of this
        var changelogs = parserService.ExtractChangelogEntries(diff.PullRequests, extraCategories);

        if (changelogs.Count == 0 && diff.RevertedPullRequestNumbers.Count == 0)
        {
            logger.LogInformation("Nothing to do");
            return true;
        }

        logger.LogInformation(
            "Generated {ChangelogCount} changelogs, {RevertedPullRequestCount} reverts found.", 
            changelogs.Count, 
            diff.RevertedPullRequestNumbers.Count
        );

        changelogWriter(changelogs, diff.RevertedPullRequestNumbers);

        return true;
    }

}