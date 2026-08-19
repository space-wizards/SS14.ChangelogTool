using LibGit2Sharp;
using SS14.ChangelogTool.LocalGit.Models;

namespace SS14.ChangelogTool.LocalGit;

/// <summary>
/// Default implementation for <see cref="ILocalGitRepository"/>.
/// </summary>
public class LocalGitRepository : ILocalGitRepository
{
    private readonly string? _repositoryDiscoveryPath;
    private IRepository? _gitRepository;

    /// <summary>
    /// Discovers the repository from the current working directory.
    /// </summary>
    public LocalGitRepository()
    {
    }

    /// <summary>
    /// Discovers the repository starting from the provided path instead of the current working directory.
    /// </summary>
    /// <param name="repositoryDiscoveryPath">Path from which <c>Repository.Discover</c> should start searching.</param>
    public LocalGitRepository(string repositoryDiscoveryPath)
    {
        _repositoryDiscoveryPath = repositoryDiscoveryPath;
    }

    private IRepository InternalRepository
    {
        get
        {
            _gitRepository ??= GetLocalRepository();
            return _gitRepository;
        }
    }

    public LastCommitData? GetLastCommitData(string filePath)
    {
        var repository = InternalRepository;

        // Do not use Commits.QueryBy(path) here: it relies on LibGit2Sharp's FileHistory, which walks the
        // whole history with a time-sorted revwalk while assuming each commit is visited after one of its
        // children. That assumption does not hold on large, merge-heavy repositories (space-station-14) and
        // crashes with KeyNotFoundException; on shallow clones it silently reports the tip commit.
        // Walk the first-parent chain instead (equivalent to `git log --first-parent -- <path>`).

        if (repository.Info.IsShallow)
            throw new InvalidOperationException(
                "Local repository is shallow (cloned with --depth). Fetch the full history first "
                + "(e.g. set fetch-depth: 0 in actions/checkout) so the last changelog change can be determined."
            );

        var normalizedPath = NormalizePath(filePath);

        var filter = new CommitFilter
        {
            IncludeReachableFrom = repository.Head.Tip,
            SortBy = CommitSortStrategies.Topological,
            FirstParentOnly = true
        };

        foreach (var commit in repository.Commits.QueryBy(filter))
        {
            var parent = commit.Parents.FirstOrDefault();
            if (parent == null)
            {
                // No parent to diff against (root of the walk). If the file exists here, it was introduced
                // by this commit.
                if (commit.Tree[normalizedPath] != null)
                    return new LastCommitData(commit.Id.Sha, commit.Committer.When);

                continue;
            }

            var changes = repository.Diff.Compare<TreeChanges>(
                parent.Tree,
                commit.Tree,
                [normalizedPath]
            );

            if (changes.Count > 0)
                return new LastCommitData(commit.Id.Sha, commit.Committer.When);
        }

        return null;
    }

    public IReadOnlyCollection<CommitBriefInfo> GetCommitsSince(string sinceSha)
    {
        var repository = InternalRepository;
        var baseCommit = repository.Lookup<Commit>(sinceSha);
        if (baseCommit == null)
        {
            throw new InvalidOperationException(
                $"Attempted to find base commit {sinceSha} to collect all changes from git history, but no such commit was found!"
            );
        }
        var filter = new CommitFilter
        {
            IncludeReachableFrom = repository.Head.Tip, // Start from the current branch tip
            ExcludeReachableFrom = baseCommit     // Stop at the target SHA (exclusive)
        };

        ICommitLog commitsSinceSha = repository.Commits.QueryBy(filter);

        return commitsSinceSha.Select(x => new CommitBriefInfo(x.Sha, x.MessageShort))
                              .ToArray();
    }

    public void Dispose()
    {
        if(_gitRepository != null)
        {
            _gitRepository.Dispose();
        }
    }

    private IRepository GetLocalRepository()
    {
        // Searches upward from the current directory (or from the explicitly provided path)
        string repoPath = Repository.Discover(_repositoryDiscoveryPath ?? ".");

        if (repoPath == null) 
            throw new InvalidOperationException("Failed to find initialized local git repository.");

        return new Repository(repoPath);

    }

    /// <summary>
    /// Normalizes a path to the form expected by git/libgit2: relative to the repository root and using forward slashes.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/')
                   .TrimStart('.', '/');
    }
}
