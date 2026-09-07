using SS14.ChangelogTool.Models.Generic;

namespace SS14.ChangelogTool.Models.Forgejo;

public record ForgejoPullRequest(
    bool Merged,
    string? Merge_commit_sha,
    string? Body,
    GenericUser? User,
    DateTimeOffset? MergedAt,
    GenericPullRequestBase? Base,
    int Number,
    string Html_url
); // : GenericPullRequest(Merged, Body, User, MergedAt, Base, Number, Html_url); This doesn't seem to work with the Html_url