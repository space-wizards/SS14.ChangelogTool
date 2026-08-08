using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.LocalGit;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Options;
using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public class ChangelogFileManager(ILocalGitRepository repository, IOptions<ChangelogToolOptions> options, ILogger<ChangelogFileManager> logger)
    : IChangelogFileManager
{
    private readonly ChangelogToolOptions _options = options.Value;
    private readonly int _maxChangelogEntries = options.Value.MaxChangelogEntries;

    /// <summary>
    /// Emojis associated with a change type
    /// </summary>
    private static readonly Dictionary<ChangeType, string> Emojis = new()
    {
        { ChangeType.Add, "🆕" },
        { ChangeType.Fix, "🐛" },
        { ChangeType.Remove, "❌" },
        { ChangeType.Tweak, "⚒️" },
    };

    /// <inheritdoc/>
    public string GetLastMergedSha(string changelogDir, IReadOnlyCollection<string>? extraCategories = null)
    {
        var allCategories = new HashSet<string> { "Changelog" };
        if (extraCategories is not null)
            allCategories.UnionWith(extraCategories);

        var lastMergedTime = DateTimeOffset.MinValue;
        var lastMergeSha = string.Empty;

        foreach (var category in allCategories)
        {
            var fileName = Path.Combine(changelogDir, $"{category}.yml");

            var lastCommitData = repository.GetLastCommitData(fileName);
            if (lastCommitData != null)
            {
                DateTimeOffset lastChangeDate = lastCommitData.When;
                if (lastMergedTime < lastChangeDate)
                {
                    lastMergedTime = lastChangeDate;
                    lastMergeSha = lastCommitData.Sha;
                }
            }
        }

        logger.LogInformation("Last PR time: {LastMergedTime}", lastMergedTime);

        if (string.IsNullOrWhiteSpace(lastMergeSha))
            throw new InvalidOperationException(
                "Attempted to get data about last merged changelog commit but found nothing!"
            );

        return lastMergeSha;
    }

    /// <inheritdoc/>
    public void DumpChangelogToMarkdown(
        string saveTo,
        Dictionary<string, List<ChangelogEntry>> changelogParts,
        string? exceptCategory
    )
    {
        using var stream = File.OpenWrite(saveTo);
        using var writer = new StreamWriter(stream);

        foreach (var (category, changelogEntries) in changelogParts)
        {
            if (exceptCategory != null && category == exceptCategory)
                continue;

            foreach (var changelogEntry in changelogEntries)
            {
                writer.WriteLine($"**{changelogEntry.Author}** updated:  ");
                foreach (var change in changelogEntry.Changes)
                {
                    var emoji = Emojis[change.Type];
                    writer.WriteLine($"{emoji} - {change.Message} ([#{changelogEntry.Number}]({changelogEntry.Url}))  ");
                }

                writer.WriteLine();
            }
        }
    }

    /// <inheritdoc/>
    public void UpdateChangelogs(
        Dictionary<string, List<ChangelogEntry>> changelogParts,
        IReadOnlyCollection<int> revertedPullRequestNumbers,
        string changelogDir)
    {
        var revertedSet = revertedPullRequestNumbers.ToHashSet();

        var deserializer = new DeserializerBuilder()
            .Build();

        var categories = new HashSet<string>{ Constants.MainCategory };
        categories.UnionWith(
            _options.ExtraCategories == null
                ? []
                : _options.ExtraCategories.Split(',')
        );

        foreach (var category in categories)
        {
            if(revertedPullRequestNumbers.Count == 0 && !changelogParts.ContainsKey(category))
                continue;

            var categoryFile = category == Constants.MainCategory
                ? "Changelog"
                : category;

            var changelogYmlPath = Path.Combine(changelogDir, $"{categoryFile}.yml");

            logger.LogInformation("Writing changelog part {ChangelogYmlPath}", changelogYmlPath);

            ChangelogContainer result;
            using (var streamToRead = File.OpenRead(changelogYmlPath))
            {
                var content = new StreamReader(streamToRead);
                result = deserializer.Deserialize<ChangelogContainer>(content);
            }

            var entries = result.Entries;

            var lastEntryId = entries.Max(x => x.Id);

            if (changelogParts.TryGetValue(category, out var changelogEntries))
            {
                foreach (var changelogEntry in changelogEntries)
                {
                    changelogEntry.Id = ++lastEntryId;
                    result.Entries.Add(changelogEntry);
                }
            }

            entries.RemoveAll(entry =>
            {
                if (!TryGetPullRequestNumber(entry.Url, out var prNumber))
                    return false;

                return revertedSet.Contains(prNumber);
            });

            var exceededBy = entries.Count - _maxChangelogEntries;
            if (exceededBy > 0)
            {
                entries = entries.Skip(exceededBy)
                    .ToList();
            }

            result.Entries = [.. entries.OrderBy(x => x.Id)];

            // Save to a string first to avoid holding multiple open handles
            using var streamToWrite = File.Open(changelogYmlPath, FileMode.Truncate, FileAccess.Write);
            using var writer = new StreamWriter(streamToWrite);
            var serializer = new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
            serializer.Serialize(writer, result);
        }
    }

    /// <summary>
    /// Tries to extract the pull request number from a changelog entry URL.
    /// GitHub PR URLs look like <c>https://github.com/owner/repo/pull/{number}</c>.
    /// </summary>
    private static bool TryGetPullRequestNumber(string url, out int prNumber)
    {
        prNumber = 0;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var lastSegment = url.TrimEnd('/').Split('/').LastOrDefault();
        return int.TryParse(lastSegment, out prNumber);
    }
}
