namespace SS14.ChangelogTool.Models.Generic;

// This can be used with GitHub directly, and Forgejo just redirects some fields to fit this standard.
public record GenericPullRequest(
    bool Merged,
    string? Body,
    GenericUser? Author,
    DateTimeOffset? MergedAt,
    GenericPullRequestBase? Base,
    int Number,
    string Url
);
