using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Clients;
using SS14.ChangelogTool.LocalGit;
using SS14.ChangelogTool.LocalGit.Models;
using SS14.ChangelogTool.Options;
using System.Text.RegularExpressions;
using SS14.ChangelogTool.Models.Generic;

namespace SS14.ChangelogTool.Services;

/// <inheritdoc/>
public partial class GenericPullRequestService(
    INetworkGitRepositoryClient networkRepositoryClient,
    ILocalGitRepository repository,
    IOptions<ChangelogToolOptions> options,
    ILogger<GenericPullRequestService> logger
) : IPullRequestService
{
    private readonly ChangelogToolOptions _options = options.Value;

    /// <summary>
    /// Matches the trailing PR reference that GitHub appends to squash merged commit messages, e.g. "... (#12345)".
    /// </summary>
    [GeneratedRegex(@"\(#(\d+)\)\s*$", RegexOptions.None)]
    private static partial Regex PullRequestNumberRegex();

    /// <summary>
    /// Detects whether a commit is a revert commit.
    /// </summary>
    [GeneratedRegex(@"\brevert\b", RegexOptions.IgnoreCase)]
    private static partial Regex RevertKeywordRegex();

    /// <summary>
    /// Matches every integer in a revert commit message, e.g. "Revert: 44644 - 40090 - 37716 - 42439 - 41004 (#44924)"
    /// yields 44644, 40090, 37716, 42439, 41004 and 44924. The caller should exclude the commit's own trailing PR number.
    /// </summary>
    [GeneratedRegex(@"\d+", RegexOptions.None)]
    private static partial Regex AnyNumberRegex();

    /// <inheritdoc/>
    public async Task<GenericDiff> GetDiff(string sinceSha)
    {
        var repo = _options.Repo;
        // we first get list of commits since provided point til HEAD
        var commitsSinceSha = repository.GetCommitsSince(sinceSha);

        if(logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Got commit history since {sha}: {history}", sinceSha, string.Join(", ", commitsSinceSha.Select(x => x.Sha)));

        // then we try to extract PR and revert numbers from commit messages
        // - its cheap and does basic filtering as we skip every commit that 
        // was not properly formatted.
        var withPullRequestNumbers = commitsSinceSha.Select(x =>
        {
            var res = ExtractPullRequestNumbers(x);
            if (res == null)
                return null;

            return new CommitAndPrNumberWrapper(x, res);

        }).Where(x=> x != null).ToArray();
        
        // then we filter commits based on repo they were added in - if such option is enabled
        var filtered = await FilterCommits(withPullRequestNumbers!, repo);

        // compose PR number list, exclude reverts.
        var pullRequestNumbers = filtered.Select(x => x.PrNum.Number)
                                         .ToHashSet();
        var revertedPullRequestNumbers = filtered.SelectMany(x => x.PrNum.Reverts).ToHashSet();
        foreach (var revertNumber in revertedPullRequestNumbers.ToArray())
        {
            // if we added PR in this changelog update - no need
            // to actually remove anything but that pr from current updates list
            if (pullRequestNumbers.Remove(revertNumber))
                revertedPullRequestNumbers.Remove(revertNumber);
        }

        logger.LogInformation(
            "Collected {count} pull request numbers and {revertCount} reverted pull request numbers since {sha}",
            pullRequestNumbers.Count,
            revertedPullRequestNumbers.Count,
            sinceSha
        );

        var pullRequests = await networkRepositoryClient.GetPullRequests(repo, pullRequestNumbers);
        pullRequests = pullRequests.OrderBy(item => item.MergedAt)
                                   .ToList();

        return new GenericDiff(pullRequests, revertedPullRequestNumbers);
    }

    private PrNumberAndRevertInfo? ExtractPullRequestNumbers(CommitBriefInfo commit)
    {
        var match = PullRequestNumberRegex().Match(commit.MessageShort);
        if (!match.Success)
        {
            logger.LogWarning(
                "Commit {CommitSha} does not have a pull request number in its message: {CommitMessage}",
                commit.Sha,
                commit.MessageShort
            );
            return null;
        }

        var number = match.Groups[1].Value;
        if (!int.TryParse(number, out var prNumber))
        {
            logger.LogWarning(
                "Commit {CommitSha} have problematic pattern in its message "
                + "- it have PR number but it is not a valid number. Commit message: {CommitMessage}",
                commit.Sha,
                commit.MessageShort
            );
            return null;
        }

        int[] reverts = [];
        if (RevertKeywordRegex().IsMatch(commit.MessageShort))
        {
            var revertedNumbers = AnyNumberRegex()
                                  .Matches(commit.MessageShort)
                                  .Select(x => x.Value)
                                  .Where(x => x != number)
                                  .Select(int.Parse);

            reverts = revertedNumbers.ToArray();
        }

        if(logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("For commit {sha} detected PR number {prNumber}.", commit.Sha, prNumber);

        return new(prNumber, reverts);
    }

    private async Task<IReadOnlyCollection<CommitAndPrNumberWrapper>> FilterCommits(IReadOnlyCollection<CommitAndPrNumberWrapper> commitsSinceSha, string repo)
    {
        if (!_options.IsProcessOnlyFromCurrentRepoEnabled)
            return commitsSinceSha;

        var shaAndPrNumber = commitsSinceSha.Select(x => (x.Commit.Sha, x.PrNum.Number))
                                            .ToArray();
        var onlyFromCurrentRepo = await networkRepositoryClient.GetCommitsIntroducedByRepo(shaAndPrNumber, repo);

        return commitsSinceSha.Where(x => onlyFromCurrentRepo.Contains(x.Commit.Sha))
                              .ToArray();
    }

    private record PrNumberAndRevertInfo(int Number, int[] Reverts);

    private record CommitAndPrNumberWrapper(CommitBriefInfo Commit, PrNumberAndRevertInfo PrNum);
}