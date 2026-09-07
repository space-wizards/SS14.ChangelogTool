using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models.Forgejo;
using SS14.ChangelogTool.Models.Generic;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Utils;

namespace SS14.ChangelogTool.Clients;

/// <inheritdoc/>
public class ForgejoGitRepositoryClient(HttpClient client, IOptions<ChangelogToolOptions> options) : INetworkGitRepositoryClient
{
    private readonly string ForgejoApiBase = $"https://{options.Value.Host}/api/v1";
    
    // We want to cache these from `GetCommitsIntroducedByRepo`
    private Dictionary<int, ForgejoPullRequest> _prCache = [];
    
    // For some reason doing this in the constructor doesn't work correctly with Html_url
    private static GenericPullRequest ForgejoToGenericPull(ForgejoPullRequest fjPull)
    {
        return new GenericPullRequest(fjPull.Merged, fjPull.Body, fjPull.User, fjPull.MergedAt,
            fjPull.Base, fjPull.Number, fjPull.Html_url);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<GenericPullRequest>> GetPullRequests(
        string repo,
        IReadOnlyCollection<int> pullRequestNumbers
    )
    {
        if (pullRequestNumbers.Count == 0)
            return [];

        var (owner, repository) = GitRepositoryUtils.ExtractParts(repo);

        var result = new List<GenericPullRequest>();

        foreach (var prNumber in pullRequestNumbers)
        {
            if (_prCache.TryGetValue(prNumber, out var pr))
            {
                result.Add(ForgejoToGenericPull(pr));
                continue;
            }
            
            var resp = await client.GetAsync($"{ForgejoApiBase}/repos/{owner}/{repository}/pulls/{prNumber}");

            if (!resp.IsSuccessStatusCode)
                continue;

            var contents = await resp.Content.ReadFromJsonAsync<ForgejoPullRequest>();
            
            if (contents is null)
                continue;

            if (!contents.Merged)
                continue;
            
            result.Add(ForgejoToGenericPull(contents));
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

        var result = new List<string>();

        foreach (var (sha, prNumber) in shaAndPrNumber)
        {
            var resp = await client.GetAsync($"{ForgejoApiBase}/repos/{owner}/{repository}/pulls/{prNumber}");

            if (!resp.IsSuccessStatusCode)
                continue;

            var contents = await resp.Content.ReadFromJsonAsync<ForgejoPullRequest>();
            
            if (contents is null)
                continue;
            
            if (!contents.Merged)
                continue;

            _prCache.Add(prNumber, contents);

            if (sha != contents.Merge_commit_sha)
                continue;

            result.Add(sha);
        }
        
        return result;
    }
}
