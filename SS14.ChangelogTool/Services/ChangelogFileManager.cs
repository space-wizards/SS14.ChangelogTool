using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models;
using System.IO.Abstractions;
using SS14.ChangelogTool.Options;
using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public class ChangelogFileManager(ILogger<ChangelogFileManager> logger, IFileSystem fileSystem, IOptions<ChangelogToolOptions> options)
    : IChangelogFileManager
{
    private readonly ChangelogToolOptions _options = options.Value;

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
    public DateTimeOffset GetLastMergedTimeFromChangelogs(string changelogDir, IReadOnlyCollection<string>? extraCategories = null)
    {
        var allCategories = new HashSet<string> { _options.PrimaryChangelog };
        if (extraCategories is not null)
            allCategories.UnionWith(extraCategories);

        var lastMergedTime = DateTimeOffset.MinValue;

        foreach (var category in allCategories)
        {
            var fileName = Path.Combine(changelogDir, $"{category}.yml");

            using var stream = fileSystem.File.OpenRead(fileName);
            using var reader = new StreamReader(stream);
            var deSerializer = new DeserializerBuilder()
                .Build();
            var container = deSerializer.Deserialize<ChangelogContainer>(reader);

            var lastMergeForCategory = DateTimeOffset.MinValue;

            foreach (var entry in container.Entries)
            {
                if(entry.Time == null)
                    continue;

                var prMergeTime = DateTimeOffset.Parse(entry.Time.Replace("\'", string.Empty));
                if (prMergeTime <= lastMergeForCategory)
                    continue;

                lastMergeForCategory = prMergeTime;
            }

            if (lastMergedTime < lastMergeForCategory)
                lastMergedTime = lastMergeForCategory;
        }

        logger.LogInformation("Last PR time: {LastMergedTime}", lastMergedTime);

        return lastMergedTime;
    }

    /// <inheritdoc/>
    public void DumpChangelogToMarkdown(
        string saveTo,
        Dictionary<string, List<ChangelogEntry>> changelogParts,
        string? exceptCategory
    )
    {
        using var stream = fileSystem.File.OpenWrite(saveTo);
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
    public void UpdateChangelogs(Dictionary<string, List<ChangelogEntry>> changelogParts, string changelogDir)
    {
        foreach (var (category, changelogEntries) in changelogParts)
        {
            var categoryFile = category == Constants.MainCategory
                ? _options.PrimaryChangelog
                : category;

            var changelogYmlPath = fileSystem.Path.Combine(changelogDir, $"{categoryFile}.yml");

            logger.LogInformation("Writing changelog part {ChangelogYmlPath}", changelogYmlPath);

            var deserializer = new DeserializerBuilder()
                .Build();

            ChangelogContainer result;
            using (var streamToRead = fileSystem.File.OpenRead(changelogYmlPath))
            {
                var content = new StreamReader(streamToRead);
                result = deserializer.Deserialize<ChangelogContainer>(content);
            }

            var entries = result.Entries;

            var lastEntryId = entries.Max(x => x.Id);

            foreach (var changelogEntry in changelogEntries)
            {
                changelogEntry.Id = ++lastEntryId;
                result.Entries.Add(changelogEntry);
            }

            var exceededBy = entries.Count - _options.MaxChangelogEntries;
            if (exceededBy > 0)
            {
                entries = entries.Skip(exceededBy)
                    .ToList();
            }

            result.Entries = [.. entries.OrderBy(x => x.Id)];

            // Save to a string first to avoid holding multiple open handles
            using var streamToWrite = fileSystem.File.Open(changelogYmlPath, FileMode.Truncate, FileAccess.Write);
            using var writer = new StreamWriter(streamToWrite);
            var serializer = new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
            serializer.Serialize(writer, result);
        }
    }
}
