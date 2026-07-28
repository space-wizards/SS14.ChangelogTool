using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Clients;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Options;
using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public class GitHubPullRequestService(
    HttpClient ghFileHttpClient,
    IGithubPullRequestClient ghPullRequestClient,
    IOptions<ChangelogToolOptions> options,
    ILogger<GitHubPullRequestService> logger
) : IGitHubPullRequestService
{
    private readonly ChangelogToolOptions _options = options.Value;
    private readonly ILogger<GitHubPullRequestService> _logger = logger;

    private const string GithubRawDownloadBase = "https://raw.githubusercontent.com";

    /// <inheritdoc/>
    public DateTimeOffset GetNewestChangelogEntryMergeDateByRef(string refSha, IReadOnlyCollection<string> extraCategories)
    {
        var lastMergedTime = DateTimeOffset.MinValue;

        var allCategories = new HashSet<string> { "Changelog" };
        allCategories.UnionWith(extraCategories);

        foreach (var category in allCategories)
        {
            var changelogContainer = GetChangelogByRef(refSha, category);
            var categoryLastMergedTime = DateTimeOffset.MinValue;
            foreach (var entry in changelogContainer.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Time))
                    continue;

                var prMergeTime = DateTimeOffset.Parse(entry.Time.Replace("\'", string.Empty));
                if (prMergeTime > categoryLastMergedTime)
                    categoryLastMergedTime = prMergeTime;
            }

            if (lastMergedTime < categoryLastMergedTime)
            {
                lastMergedTime = categoryLastMergedTime;
            }
        }

        return lastMergedTime;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<GitHubPullRequest>> GetDiff(DateTimeOffset olderThen)
    {
        var repo = _options.Repo;
        var branch = _options.Branch;

        var pullRequests = await ghPullRequestClient.GetPullRequestsOlderThen(repo, branch, olderThen);

        pullRequests = pullRequests.OrderBy(item => item.MergedAt!.Value)
            .ToList();

        return pullRequests;
    }

    private ChangelogContainer GetChangelogByRef(string sinceRefSha, string category)
    {
        var refChangelogUrl = $"{GithubRawDownloadBase}/{_options.Repo}/{sinceRefSha}/{_options.ChangelogRepoPath}/{category}.yml";
        HttpRequestMessage request = new(HttpMethod.Get, refChangelogUrl);
        request.Headers.Add("Authorization", $"Bearer {_options.GithubToken}");
        var response = ghFileHttpClient.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Could not get changelog content: " + response.Content.ReadAsStringAsync().Result);
        }

        using var reader = new StreamReader(response.Content.ReadAsStream());
        var deserializer = new DeserializerBuilder()
            .Build();

        return deserializer.Deserialize<ChangelogContainer>(reader);
    }
}
