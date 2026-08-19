using LibGit2Sharp;
using SS14.ChangelogTool.LocalGit;

namespace SS14.ChangelogTool.Tests;

public class LocalGitRepositoryTest : IDisposable
{
    private readonly HashSet<string> _tempPaths = [];

    [Fact]
    public void GetLastCommitData_ReturnsLastCommitThatActuallyChangedTheFile()
    {
        var repoPath = CreateTempRepository();

        string changelogPath = Path.Combine(repoPath, "Resources", "Changelog", "Changelog.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(changelogPath)!);

        string commitASha;
        string commitBSha;
        string commitCSha;
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        using (var repo = new Repository(repoPath))
        {
            // Commit A: introduce the changelog file.
            File.WriteAllText(changelogPath, "version 1\n");
            LibGit2Sharp.Commands.Stage(repo, "Resources/Changelog/Changelog.yml");
            commitASha = repo.Commit("changelog: add", signature, signature).Sha;

            // Commit B: modify the changelog file.
            File.WriteAllText(changelogPath, "version 2\n");
            LibGit2Sharp.Commands.Stage(repo, "Resources/Changelog/Changelog.yml");
            commitBSha = repo.Commit("changelog: update", signature, signature).Sha;

            // Commit C: unrelated change that must NOT be reported as the last changelog change.
            File.WriteAllText(Path.Combine(repoPath, "other.txt"), "hello\n");
            LibGit2Sharp.Commands.Stage(repo, "other.txt");
            commitCSha = repo.Commit("other: unrelated", signature, signature).Sha;
        }

        using var localRepository = new LocalGitRepository(repoPath);
        var data = localRepository.GetLastCommitData("Resources/Changelog/Changelog.yml");

        Assert.NotNull(data);
        Assert.Equal(commitBSha, data.Sha);
        Assert.NotEqual(commitASha, data.Sha);
        Assert.NotEqual(commitCSha, data.Sha);
    }

    [Fact]
    public void GetLastCommitData_ReturnsNull_WhenFileWasNeverChanged()
    {
        var repoPath = CreateTempRepository();
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        using (var repo = new Repository(repoPath))
        {
            File.WriteAllText(Path.Combine(repoPath, "other.txt"), "hello\n");
            LibGit2Sharp.Commands.Stage(repo, "other.txt");
            repo.Commit("initial", signature, signature);
        }

        using var localRepository = new LocalGitRepository(repoPath);
        var data = localRepository.GetLastCommitData("Resources/Changelog/Changelog.yml");

        Assert.Null(data);
    }

    [Fact]
    public void GetLastCommitData_ShallowRepository_ThrowsClearError()
    {
        var repoPath = CreateTempRepository();

        string changelogPath = Path.Combine(repoPath, "Resources", "Changelog", "Changelog.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(changelogPath)!);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        using (var repo = new Repository(repoPath))
        {
            File.WriteAllText(changelogPath, "version 1\n");
            LibGit2Sharp.Commands.Stage(repo, "Resources/Changelog/Changelog.yml");
            var commit = repo.Commit("changelog: add", signature, signature);

            // Simulate a shallow clone by marking the tip commit as a shallow boundary.
            File.WriteAllText(Path.Combine(repoPath, ".git", "shallow"), commit.Sha + "\n");
        }

        using var localRepository = new LocalGitRepository(repoPath);
        var exception = Assert.Throws<InvalidOperationException>(
            () => localRepository.GetLastCommitData("Resources/Changelog/Changelog.yml")
        );

        Assert.Contains("shallow", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetLastCommitData_HandlesWindowsStylePathSeparators()
    {
        var repoPath = CreateTempRepository();

        string changelogPath = Path.Combine(repoPath, "Resources", "Changelog", "Changelog.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(changelogPath)!);

        string expectedSha;
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        using (var repo = new Repository(repoPath))
        {
            File.WriteAllText(changelogPath, "version 1\n");
            LibGit2Sharp.Commands.Stage(repo, "Resources/Changelog/Changelog.yml");
            repo.Commit("changelog: add", signature, signature);

            File.WriteAllText(changelogPath, "version 2\n");
            LibGit2Sharp.Commands.Stage(repo, "Resources/Changelog/Changelog.yml");
            expectedSha = repo.Commit("changelog: update", signature, signature).Sha;
        }

        using var localRepository = new LocalGitRepository(repoPath);
        var data = localRepository.GetLastCommitData(@"Resources\Changelog\Changelog.yml");

        Assert.NotNull(data);
        Assert.Equal(expectedSha, data.Sha);
    }

    private string CreateTempRepository()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ss14_localgit_test_" + Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        Directory.CreateDirectory(tempPath);

        // Init returns the path to the created repository; open it lazily via LocalGitRepository.
        Repository.Init(tempPath);

        return tempPath;
    }

    public void Dispose()
    {
        foreach (var tempPath in _tempPaths)
        {
            if (Path.Exists(tempPath))
            {
                ClearReadOnlyAttributes(tempPath);
                Directory.Delete(tempPath, true);
            }
        }
    }

    /// <summary>
    /// Git writes loose objects with read-only attributes on Windows, which would make <see cref="Directory.Delete(string, bool)"/>
    /// fail with <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    private static void ClearReadOnlyAttributes(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }
    }
}
