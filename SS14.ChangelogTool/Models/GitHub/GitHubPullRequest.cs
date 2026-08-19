namespace SS14.ChangelogTool.Models.GitHub;

public sealed record GitHubPullRequest(
    bool Merged,
    string? Body,
    GitHubUser? User,
    DateTimeOffset? MergedAt,
    GitHubPullRequestBase? Base,
    int Number,
    string Html_url
);

public sealed class GitHubPullRequestsResponse
{
    public Dictionary<string, GitHubPullRequest?> Repository { get; set; } = [];
}