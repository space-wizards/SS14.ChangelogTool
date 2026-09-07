using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models.Generic;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Utils;

namespace SS14.ChangelogTool.Clients;

/// <inheritdoc/>
public class GithubGraphQLClient(
    IGraphQLClient graphQlClient, 
    IOptions<ChangelogToolOptions> options, 
    ILogger<GithubGraphQLClient> logger
) : INetworkGitRepositoryClient
{
    public const string GithubGraphQLApiBase = "https://api.github.com/graphql";

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<GenericPullRequest>> GetPullRequests(
        string repo,
        IReadOnlyCollection<int> pullRequestNumbers
    )
    {
        if (pullRequestNumbers.Count == 0)
            return [];

        var (owner, repository) = GitRepositoryUtils.ExtractParts(repo);

        var batchSize = options.Value.MaxPullRequestEntriesInGraphQLRequest;

        var result = new List<GenericPullRequest>();

        var prNumberChunk = pullRequestNumbers.Distinct()
                                              .Chunk(batchSize);
        foreach (var batch in prNumberChunk)
        {
            var pullRequestFields = string.Join(
                "\n",
                batch.Select(number => $$"""
                                         pr{{number}}: pullRequest(number: {{number}}) {
                                           merged
                                           body
                                           author {
                                             login
                                           }
                                           mergedAt
                                           baseRef {
                                             name
                                           }
                                           number
                                           url
                                         }
                                         """
                )
            );

            var query = $$"""
                          {
                            repository(owner: "{{owner}}", name: "{{repository}}") {
                              {{pullRequestFields}}
                            }
                          }
                          """;

            var request = new GraphQLRequest(query);

            var response = await graphQlClient.SendQueryAsync<GitHubPullRequestsResponse>(request);

            EnsureSuccessful(response);

            result.AddRange(response.Data.Repository.Values.Where(x => x is not null)!);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetCommitsIntroducedByRepo(
        IReadOnlyCollection<(string Sha, int PullRequestNumber)> shaAndPrNumber,
        string repo
    )
    {
        if (shaAndPrNumber.Count == 0)
            return [];

        var (owner, repository) = GitRepositoryUtils.ExtractParts(repo);

        var chunkSize = options.Value.MaxCommitEntriesInGraphQLRequest;
        var chunks = shaAndPrNumber.DistinctBy(x => x.Sha)
                                   .Chunk(chunkSize);

        var result = new List<string>();

        foreach (var chunk in chunks)
        {
            var queryParts = new List<string>();
            for (int i = 0; i < chunk.Length; i++)
            {
                queryParts.Add(
                    $$"""
                      commit_{{i}}: object(oid: "{{chunk[i].Sha}}") {
                          ... on Commit {
                              associatedPullRequests(first: 1) {
                                  nodes {
                                      number
                                      mergeCommit {
                                          oid
                                      }
                                  }
                              }
                          }
                      }
                      """
                );
            }

            var query = $$"""
                          {
                            repository(owner: "{{owner}}", name: "{{repository}}") {
                              {{string.Join("\n", queryParts)}}
                            }
                          }
                          """;

            var request = new GraphQLRequest(query);

            var response = await graphQlClient.SendQueryAsync<RepositoryCommitSearchResponse>(request);

            EnsureSuccessful(response);

            // A commit was introduced by the repository iff some pull request in it was merged from exactly
            // this SHA and its number matches the one referenced by the commit message.
            for (var i = 0; i < chunk.Length; i++)
            {
                var currentSha = chunk[i];

                if (!response.Data.Repository.TryGetValue($"commit_{i}", out var commitObject)
                    || commitObject?.AssociatedPullRequests is null)
                {
                    logger.LogTrace(
                        "Failed to resolve commit with SHA {sha} while detecting its origin, "
                        + "probably means it is not originating from {repo} repository.", 
                        currentSha, 
                        repo
                    );
                    continue;
                }

                var isIntroducedByRepo = commitObject.AssociatedPullRequests.Nodes.Any(
                    pr => pr.MergeCommit?.Oid == currentSha.Sha && pr.Number == currentSha.PullRequestNumber
                );
                if (isIntroducedByRepo)
                {
                    result.Add(currentSha.Sha);
                    logger.LogTrace("Commit {sha} is from {repo}", currentSha, repo);
                }
                else
                {
                    logger.LogTrace("Commit {sha} is not from {repo}", currentSha, repo);
                }
            }
        }

        return result;
    }

    private static void EnsureSuccessful<T>(GraphQLResponse<T> response)
    {
        if (response.Errors is { Length: > 0 })
        {
            throw new InvalidOperationException(
                "GitHub GraphQL request failed when discovering commit owners: "
                + string.Join("; ", response.Errors.Select(e => e.Message))
            );
        }
    }
}