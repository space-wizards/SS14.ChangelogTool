using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SS14.ChangelogTool.Clients;
using SS14.ChangelogTool.LocalGit;
using SS14.ChangelogTool.LocalGit.Models;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;

namespace SS14.ChangelogTool.Tests;

public class GitHubPullRequestServiceTests
{
    private const string Repo = "space-wizards/space-station-14";
    private const string SinceSha = "base-sha";

    private readonly IGithubPullRequestClient _client;
    private readonly ILocalGitRepository _repository;
    private readonly GitHubPullRequestService _cut;

    public GitHubPullRequestServiceTests()
    {
        _client = Substitute.For<IGithubPullRequestClient>();
        _repository = Substitute.For<ILocalGitRepository>();

        _client.GetPullRequests(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<int>>())
               .Returns([]);

        _cut = new GitHubPullRequestService(
            _client,
            _repository,
            CreateOptions(),
            NullLogger<GitHubPullRequestService>.Instance);
    }

    [Theory]
    [InlineData("[STAGING] Revert: 44644 - 40090 - 37716 - 42439 - 41004 (#44924)", new[] { 44644, 40090, 37716, 42439, 41004 }, 44924)]
    [InlineData("[STAGING] Revert 36673 and Fix Changelog (#44929)", new[] { 36673 }, 44929)]
    public async Task GetDiff_HasRevertCommit_PutsRevertedNumbersInRevertedList(string commitMessage, int[] expectedReverted, int selfPullRequestNumber)
    {
        // Arrange
        _repository.GetCommitsSince(SinceSha)
                   .Returns([new CommitBriefInfo("some-sha", commitMessage)]);

        // Act
        var diff = await _cut.GetDiff(SinceSha);

        // Assert: the reverted PR numbers are collected, and the revert PR itself is not among them
        Assert.Equal(expectedReverted.OrderBy(x => x), diff.RevertedPullRequestNumbers.OrderBy(x => x));
        Assert.All(expectedReverted, n => Assert.DoesNotContain(n, diff.PullRequests.Select(pr => pr.Number)));

        // The revert PR's own number should still be requested from the client
        await _client.Received(1).GetPullRequests(
            Repo,
            Arg.Is<IReadOnlyCollection<int>>(numbers => numbers != null && numbers.Contains(selfPullRequestNumber)));
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

    private static IOptions<ChangelogToolOptions> CreateOptions() =>
        Microsoft.Extensions.Options.Options.Create<ChangelogToolOptions>(new()
    {
        Repo = Repo,
        ChangelogRepoPath = "Resources/Changelog",
        GithubToken = "fake-token",
    });
}