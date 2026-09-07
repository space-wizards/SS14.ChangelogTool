using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using SS14.ChangelogTool.Clients;
using SS14.ChangelogTool.LocalGit;
using SS14.ChangelogTool.LocalGit.Models;
using SS14.ChangelogTool.Models.Generic;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;

namespace SS14.ChangelogTool.Tests;

public class GenericPullRequestServiceTests
{
    private readonly ChangelogToolOptions _changelogToolOptions = new()
    {
        Repo = Repo,
        ChangelogRepoPath = "Resources/Changelog",
        GithubToken = "fake-token",
        IsProcessOnlyFromCurrentRepoEnabled = false
    };

    private const string Repo = "space-wizards/space-station-14";
    private const string SinceSha = "base-sha";

    private readonly INetworkGitRepositoryClient _client;
    private readonly ILocalGitRepository _repository;
    private readonly GenericPullRequestService _cut;

    public GenericPullRequestServiceTests()
    {
        _client = Substitute.For<INetworkGitRepositoryClient>();
        _repository = Substitute.For<ILocalGitRepository>();

        _client.GetPullRequests(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<int>>())
               .Returns([]);

        _cut = new GenericPullRequestService(
            _client,
            _repository,
            Microsoft.Extensions.Options.Options.Create(_changelogToolOptions),
            NullLogger<GenericPullRequestService>.Instance
        );
    }

    [Theory]
    [InlineData("[STAGING] Revert: 44644 - 40090 - 37716 - 42439 - 41004 (#44924)", new[] { 44644, 40090, 37716, 42439, 41004 })]
    [InlineData("[STAGING] Revert 36673 and Fix Changelog (#44929)", new[] { 36673 })]
    public async Task GetDiff_HasRevertCommit_PutsRevertedNumbersInRevertedList(string commitMessage, int[] expectedReverted)
    {
        // Arrange
        _repository.GetCommitsSince(SinceSha)
                   .Returns([new CommitBriefInfo("some-sha", commitMessage)]);

        // Act
        var diff = await _cut.GetDiff(SinceSha);

        // Assert: the reverted PR numbers are collected, and the revert PR itself is not among them
        Assert.Equal(expectedReverted.OrderBy(x => x), diff.RevertedPullRequestNumbers.OrderBy(x => x));
        Assert.All(expectedReverted, n => Assert.DoesNotContain(n, diff.PullRequests.Select(pr => pr.Number)));
    }

    [Fact]
    public async Task GetDiff_NoRevertCommits_DoesNotReportAnyRevertedPullRequests()
    {
        // Arrange
        _repository.GetCommitsSince(SinceSha)
                   .Returns([new CommitBriefInfo("some-sha", "Some normal change (#5234)")]);

        // Act
        var diff = await _cut.GetDiff(SinceSha);

        // Assert
        Assert.Empty(diff.RevertedPullRequestNumbers);
    }

    [Fact]
    public async Task GetDiff_FilterCommitsBasedOnRepo_OnlyCurrentRepoCommitsPass()
    {
        // Arrange

        _changelogToolOptions.IsProcessOnlyFromCurrentRepoEnabled = true;
        _changelogToolOptions.Repo = "My/custom-repo";

        _repository.GetCommitsSince(SinceSha)
                   .Returns([
                       new CommitBriefInfo("some-sha1", "Some normal change (#5234)"),
                       new CommitBriefInfo("some-sha2", "Some normal change (#2)"),
                   ]);

        _client.ClearSubstitute();

        _client.GetCommitsIntroducedByRepo(Arg.Any<IReadOnlyCollection<(string, int)>>(), Arg.Any<string>())
               .Returns(["some-sha2"]);

        _client.GetPullRequests(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<int>>())
               .Returns(
                   args => args.ArgAt<IReadOnlyCollection<int>>(1)
                               .Select(PullRequestFactory)
                               .ToArray()
                );

        // Act
        var diff = await _cut.GetDiff(SinceSha);

        // Assert
        Assert.DoesNotContain(5234, diff.PullRequests.Select(x => x.Number));
        Assert.Contains(2, diff.PullRequests.Select(x => x.Number));
    }

    private static GenericPullRequest PullRequestFactory(int pullRequestNumber)
    {
        return new GenericPullRequest(true, "some-buddy", new GenericUser("sm1"), new DateTimeOffset(), new GenericPullRequestBase("ref"), pullRequestNumber, "some-url");
    }
}