using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Options;

namespace SS14.ChangelogTool.Services;

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
    /// based on <see cref="lastChangeProvider"/> date (and data given by github)
    /// and writes data using <see cref="changelogWriter"/>.
    /// </summary>
    public async Task<bool> TryGenerate(
        Func<IReadOnlyCollection<string>, DateTimeOffset> lastChangeProvider,
        Action<Dictionary<string, List<ChangelogEntry>>> changelogWriter
    )
    {
        List<string> extraCategories = [];
        if (_options.ExtraCategories is not null)
            extraCategories.AddRange(_options.ExtraCategories.Split(','));

        // Get the last merged PR time
        var lastMergedTime = lastChangeProvider(extraCategories);

        logger.LogInformation("Generating diff from {LastMergedTime}", lastMergedTime);

        // Get the list of PRs that were merged since last time.
        var diff = await githubService.GetDiff(lastMergedTime);

        logger.LogInformation("Collected {PullRequestCount} pull requests", diff.Count);

        // Generate a new YMLfest out of this
        var changelogs = parserService.ExtractChangelogEntries(diff, extraCategories);

        if (changelogs.Count == 0)
        {
            logger.LogInformation("Nothing to do");
            return true;
        }

        logger.LogInformation("Generated {ChangelogCount} changelogs", changelogs.Count);

        // Add these parts to the actual changelog and trim older entries
        changelogWriter(changelogs);

        return true;
    }

}