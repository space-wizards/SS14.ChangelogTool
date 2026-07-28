using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Models.GraphQL;
using SS14.ChangelogTool.Options;

namespace SS14.ChangelogTool.Clients;

/// <inheritdoc/>
public class GithubPullRequestClient(IGraphQLClient graphQlClient, IOptions<ChangelogToolOptions> options) : IGithubPullRequestClient
{
    public const string GithubGraphQLApiBase = "https://api.github.com/graphql";

    private readonly ChangelogToolOptions _options = options.Value;
    
    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<GitHubPullRequest>> GetPullRequestsOlderThen(string repo, string branch, DateTimeOffset olderThen)
    {
        var page = 0;
        string? afterCursor = null;

        var date = olderThen.ToString("yyyy-MM-dd");
        var pullRequests = new List<GitHubPullRequest>();
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

            var request = new GraphQLRequest(query);
            var response = await graphQlClient.SendQueryAsync<GraphQLResponse>(request);

            pullRequests.AddRange(
                response.Data.Search.Edges
                    .Where(edge => edge.Node.MergedAt > olderThen)
                    .Select(edge => edge.Node)
            );

            if (!response.Data.Search.PageInfo.HasNextPage)
                break;

            afterCursor = response.Data.Search.PageInfo.EndCursor;
        }

        return pullRequests;
    }
}