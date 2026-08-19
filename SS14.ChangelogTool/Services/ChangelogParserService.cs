using Microsoft.Extensions.Logging;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Models.GitHub;
using System.Text.RegularExpressions;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public partial class ChangelogParserService(ILogger<ChangelogParserService> logger) : IPullRequestParserService
{
    [GeneratedRegex(@"^\s*(?::cl:|🆑) *([a-z0-9_\- ,&]+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ChangelogHeaderRegex();

    [GeneratedRegex(@"^ *[*-]? *(add|remove|tweak|fix|bug|bugfix): *([^\n\r]+)\r?$", RegexOptions.IgnoreCase)]
    private static partial Regex ChangelogEntryRegex();

    [GeneratedRegex(@"^\s*([a-z]+):\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ChangelogCategoryRegex();

    [GeneratedRegex(@"(?<!\\)<!--([^>]+)(?<!\\)-->", RegexOptions.None)]
    private static partial Regex CommentRegex();

    /// <inheritdoc/>
    public Dictionary<string, List<ChangelogEntry>> ExtractChangelogEntries(
        IEnumerable<GitHubPullRequest> pullRequests,
        List<string>? extraCategories = null
    )
    {
        var changesByCategory = new Dictionary<string, List<ChangelogEntry>>();
        foreach (var pr in pullRequests)
        {
            var parsed = ParsePrBody(pr, extraCategories ?? []);
            foreach (var (category, changelogEntry) in parsed)
            {
                if (!changesByCategory.TryGetValue(category, out var list))
                {
                    list = [];
                    changesByCategory[category] = list;
                }
                list.Add(changelogEntry);
            }
        }

        return changesByCategory;
    }


    public static Dictionary<string, ChangelogEntry> ParsePrBody(GitHubPullRequest pr, IReadOnlyCollection<string> extraCategories)
    {
        var allCategories = new HashSet<string> { Constants.MainCategory };
        allCategories.UnionWith(extraCategories);

        var body = CommentRegex().Replace(pr.Body ?? "", "");

        var match = ChangelogHeaderRegex().Match(body);
        if (!match.Success)
            return [];

        // GitHub returns "author: null" for pull requests whose author account was deleted,
        // which deserializes User to null; fall back to a placeholder in that case.
        var author = match.Groups[1].Success ? match.Groups[1].Value.Trim() : pr.User?.Login ?? "Unknown";
        var changelogBody = body.Substring(match.Index + match.Length);

        var currentCategory = Constants.MainCategory;
        var entries = new List<(string Category, ChangeDescription ChangeDone)>();

        var reader = new StringReader(changelogBody);
        while (reader.ReadLine() is { } line)
        {
            var categoryMatch = ChangelogCategoryRegex().Match(line);
            if (categoryMatch.Success)
            {
                var categoryName = categoryMatch.Groups[1].Value;
                var correctedName = categoryName.ToUpperInvariant() switch
                {
                    "ADMIN" => "Admin",
                    "MAPS" => "Maps",
                    "RULES" => "Rules",
                    _ => Constants.MainCategory,
                };

                if (allCategories.TryGetValue(correctedName, out var matchedCategory))
                    currentCategory = matchedCategory;

                continue;
            }

            var entryMatch = ChangelogEntryRegex().Match(line);
            if (!entryMatch.Success)
                continue;

            var type = entryMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                "add" => ChangeType.Add,
                "remove" => ChangeType.Remove,
                "fix" or "bugfix" or "bug" => ChangeType.Fix,
                "tweak" => ChangeType.Tweak,
                _ => (ChangeType?)null,
            };

            var message = entryMatch.Groups[2].Value.Trim();

            if (type is { } t)
                entries.Add((currentCategory, new ChangeDescription(t, message)));
        }

        return entries
            .GroupBy(e => e.Category)
            .ToDictionary(
                x => x.Key, 
                x => new ChangelogEntry
                {
                    Number = pr.Number,
                    Url = pr.Html_url,
                    Author = author,
                    Changes = x.Select(c => c.ChangeDone)
                        .ToList(),
                    Time = (pr.MergedAt ?? DateTimeOffset.Now).ToString("O")
                }
            );
    }
}
