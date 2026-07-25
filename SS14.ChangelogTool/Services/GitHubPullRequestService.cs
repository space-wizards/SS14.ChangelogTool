using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Models.GraphQL;
using SS14.ChangelogTool.Options;
using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public class GitHubPullRequestService(
    HttpClient ghHttpClient,
    IOptions<ChangelogConfigOptions> options,
    ILogger<GitHubPullRequestService> logger
) : IGitHubPullRequestService
{
    private static readonly SystemTextJsonSerializer SystemTextJsonSerializer = new();
    private readonly ChangelogConfigOptions _options = options.Value;
    private readonly ILogger<GitHubPullRequestService> _logger = logger;

    private const string GithubGraphQLApiBase = "https://api.github.com/graphql";
    private const string GithubRawDownloadBase = "https://raw.githubusercontent.com";

    /// <inheritdoc/>
    public DateTimeOffset GetLastMergedFromRef(string sinceRefSha, IReadOnlyCollection<string> extraCategories)
    {
        var lastMergedTime = DateTimeOffset.MinValue;

        var allCategories = new List<string> { "Changelog" };
        allCategories.AddRange(extraCategories);

        foreach (var category in allCategories)
        {
            var changelogContainer = GetChangelogByRef(sinceRefSha, category);
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
        var pullRequests = new List<GitHubPullRequest>();

        var date = olderThen.ToString("yyyy-MM-dd");
        var page = 0;
        string? afterCursor = null;

        var repo = _options.Repo;
        var branch = _options.Branch;
        var token = _options.GithubToken;

        while (page <= _options.MaxPages)
        {
            page++;

            string afterCursorString = afterCursor is null ? "null" : $"\"{afterCursor}\"";

            var query = $$"""
                          {
                            search(first: 50, query: "is:pr repo:{{repo}} base:{{branch}} is:merged merged:>={{date}}", type: ISSUE, after: {{afterCursorString}}) {
                              edges {
                                node {
                                  ... on PullRequest {
                                    merged
                                    body
                                    user: author {
                                      login
                                    }
                                    mergedAt
                                    base: baseRef {
                                      ref: name
                                    }
                                    number
                                    html_url: url
                                  }
                                }
                              }
                              pageInfo {
                                hasNextPage
                                endCursor
                              }
                            }
                          }
                          """;

            var client = new GraphQLHttpClient(GithubGraphQLApiBase, SystemTextJsonSerializer, ghHttpClient);
            client.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var request = new GraphQLRequest(query);
            var response = await client.SendQueryAsync<GraphQLResponse>(request);

            foreach (var edge in response.Data.Search.Edges)
            {
                if (edge.Node.MergedAt <= olderThen)
                    continue;

                pullRequests.Add(edge.Node);
            }

            if (!response.Data.Search.PageInfo.HasNextPage)
                break;

            afterCursor = response.Data.Search.PageInfo.EndCursor;
        }

        pullRequests = pullRequests.OrderBy(item => item.MergedAt!.Value)
            .ToList();

        return pullRequests;
    }

    private ChangelogContainer GetChangelogByRef(string sinceRefSha, string category)
    {
        var refChangelogUrl = $"{GithubRawDownloadBase}/{_options.Repo}/{sinceRefSha}/{_options.ChangelogRepoPath}/{category}.yml";
        HttpRequestMessage request = new(HttpMethod.Get, refChangelogUrl);
        request.Headers.Add("Authorization", $"Bearer {_options.GithubToken}");
        var response = ghHttpClient.Send(request);
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
