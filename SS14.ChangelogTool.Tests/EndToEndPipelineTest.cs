using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;
using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.LocalGit;
using SS14.ChangelogTool.Tests.TestInfrastructure;
using Xunit.Abstractions;
using SS14.ChangelogTool.LocalGit.Models;

namespace SS14.ChangelogTool.Tests;

public class EndToEndPipelineTest(ITestOutputHelper outputHelper) : IDisposable
{
    private readonly HashSet<string> _tempPaths = [];

    private readonly InvocationConfiguration _invocationConfiguration = new()
    {
        EnableDefaultExceptionHandler = false
    };

    #region UpdateCommand

    [Fact]
    public void UpdateCommand_HaveNewEntries_WritesNewEntryToChangelog()
    {
        // Arrange: use the full DI setup from Registry, then override test-specific parts
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new(lastChangeSha, "fgdfgs (#5234)")]);

        // Stub out the GitHub service so it doesn't try to make real HTTP calls
        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [
                         new GitHubPullRequest(
                             Merged: true,
                             """
                             Adds the cool feature!
 
                             :cl:
                             - add: Integration test feature
                             """,
                             new GitHubUser("TestUser"),
                             new DateTimeOffset(new DateTime(2022,12,5,12,3,5), TimeSpan.Zero),
                             new GitHubPullRequestBase("master"),
                             Number: 42,
                             "https://example.com/pr/42"
                         )
                     ],
                     []
                 ));
        services.AddSingleton(ghService);

        // Use temp directory with resource files for this integration-style test
        var virtualDir = CopyExistingChangelogs();

        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act: invoke UpdateCommand just like the real program does
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // the Changelog.yml now contains our integration test change
        var changelogPath = Path.Combine(virtualDir, "Changelog.yml");
        var updatedContent = File.ReadAllText(changelogPath);

        // Verify pre-existing entries and new ones are still there
        const string oldEntryExistingAfterRolling =
            """
            - author: ThatGuyUSA
              changes:
              - type: Add
                message: There are more IDs and icons that can be used for a variety of roles.
              id: 9355
              time: '2026-01-06T10:41:32.0000000+00:00'
              url: https://github.com/space-wizards/space-station-14/pull/42200
            """;

        Assert.Contains(oldEntryExistingAfterRolling, updatedContent);

        const string expectedEntry = """
                                     - author: TestUser
                                       changes:
                                       - type: Add
                                         message: Integration test feature
                                       id: 9862
                                       time: '2022-12-05T12:03:05.0000000+00:00'
                                       url: https://example.com/pr/42
                                     
                                     """;
        Assert.EndsWith(expectedEntry, updatedContent);
    }

    [Fact]
    public void UpdateCommand_HaveMoreThenLimitOfChangeLogEntries_PrunesOldEntries()
    {
        // Arrange: configure a very small max entries limit so rolling kicks in
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services, maxLogEntries: 5);

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new("some-sha", "fgdfgs (#5234)")]);

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [
                         new GitHubPullRequest(true,
                             ":cl: \n- add: Fresh new entry",
                             new GitHubUser("NewUser"),
                             new DateTimeOffset(new DateTime(2022,12,5,12,3,5), TimeSpan.Zero),
                             new GitHubPullRequestBase("master"),
                             999,
                             "https://example.com/pr/999")
                     ],
                     []
                 ));
        services.AddSingleton(ghService);


        // Create a changelog with 5 old entries (at the rolling boundary)
        var tempPath = Path.Combine(Path.GetTempPath(), "ss14_changelog_rolling_" + Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        Directory.CreateDirectory(tempPath);

        const string oldYaml = """
                               Entries:
                               - author: OldUser
                                 changes:
                                 - type: Add
                                   message: Oldest entry
                                 id: 1
                                 time: '2020-01-01T00:00:00.0000000+00:00'
                                 url: https://example.com/pr/1
                               - author: OldUser
                                 changes:
                                 - type: Add
                                   message: Second oldest
                                 id: 2
                                 time: '2020-06-01T00:00:00.0000000+00:00'
                                 url: https://example.com/pr/2
                               - author: OldUser
                                 changes:
                                 - type: Add
                                   message: Middle entry
                                 id: 3
                                 time: '2021-01-01T00:00:00.0000000+00:00'
                                 url: https://example.com/pr/3
                               - author: OldUser
                                 changes:
                                 - type: Add
                                   message: Fourth entry
                                 id: 4
                                 time: '2021-06-01T00:00:00.0000000+00:00'
                                 url: https://example.com/pr/4
                               - author: OldUser
                                 changes:
                                 - type: Add
                                   message: Fifth entry (last before roll)
                                 id: 5
                                 time: '2022-01-01T00:00:00.0000000+00:00'
                                 url: https://example.com/pr/5
                               """;
        File.WriteAllText(Path.Combine(tempPath, "Changelog.yml"), oldYaml);

        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"update --changelog-dir \"{tempPath}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // oldest entry was pruned, newest was added, max is 5
        var changelogPath = Path.Combine(tempPath, "Changelog.yml");
        var updatedContent = File.ReadAllText(changelogPath);

        // Oldest entry should be gone
        Assert.DoesNotContain("Oldest entry", updatedContent);
        // New entry should be present
        Assert.Contains("Fresh new entry", updatedContent);
        // Still only 5 total entries (rolled)
        var entryCount = Regex.Matches(updatedContent, "- author:").Count;
        Assert.Equal(5, entryCount);
    }

    [Fact]
    public void UpdateCommand_WithMultipleCategories_WritesToSeparateFiles()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();
        OverrideOptions(services, extraCategories: "Admin,Maps");

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new("some-sha", "fgdfgs (#5234)")]);

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [
                         new GitHubPullRequest(
                             Merged: true,
                             """
                             Multi-category PR!

                             :cl:
                             - add: Added to main category
                             admin:
                             - fix: Fixed admin stuff
                             maps:
                             - tweak: Tweaked map
                             """,
                             new GitHubUser("CategoryUser"),
                             new DateTimeOffset(new DateTime(2024,1,15,8,0,0), TimeSpan.Zero),
                             new GitHubPullRequestBase("master"),
                             Number: 200,
                             "https://example.com/pr/200"
                         )
                     ],
                     []
                 ));
        services.AddSingleton(ghService);


        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // Main changelog contains entry
        var changelogContent = File.ReadAllText(Path.Combine(virtualDir, "Changelog.yml"));
        outputHelper.WriteLine(changelogContent);
        Assert.Contains(
            """
            - author: CategoryUser
              changes:
              - type: Add
                message: Added to main category
              id: 9862
              time: '2024-01-15T08:00:00.0000000+00:00'
              url: https://example.com/pr/200
            """,
            changelogContent
        );

        // Admin.yml should have been created/updated
        var adminPath = Path.Combine(virtualDir, "Admin.yml");
        Assert.True(File.Exists(adminPath));
        var adminContent = File.ReadAllText(adminPath);
        Assert.Contains(
            """
            - author: CategoryUser
              changes:
              - type: Fix
                message: Fixed admin stuff
              id: 232
              time: '2024-01-15T08:00:00.0000000+00:00'
              url: https://example.com/pr/200
            """,
            adminContent
        );

        // Maps.yml should have been created/updated
        var mapsPath = Path.Combine(virtualDir, "Maps.yml");
        Assert.True(File.Exists(mapsPath));
        var mapsContent = File.ReadAllText(mapsPath);
        Assert.Contains(
            """
            - author: CategoryUser
              changes:
              - type: Tweak
                message: Tweaked map
              id: 150
              time: '2024-01-15T08:00:00.0000000+00:00'
              url: https://example.com/pr/200
            """,
            mapsContent
        );
    }

    [Fact]
    public void UpdateCommand_WithPullRequestHaveNoChanges_DoesNothing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new("some-sha", "fgdfgs (#5234)")]);

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [
                         new GitHubPullRequest(
                             Merged: true,
                             """
                             This PR has no changelog header at all.
                             Just some regular description.
                             """,
                             new GitHubUser("NoClUser"),
                             new DateTimeOffset(new DateTime(2023,3,10,14,0,0), TimeSpan.Zero),
                             new GitHubPullRequestBase("master"),
                             Number: 101,
                             "https://example.com/pr/101"
                         )
                     ],
                     []
                 ));
        services.AddSingleton(ghService);


        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Grab original content for comparison
        var changelogPath = Path.Combine(virtualDir, "Changelog.yml");
        var originalContent = File.ReadAllText(changelogPath);

        // Act
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // file should be unchanged
        var updatedContent = File.ReadAllText(changelogPath);
        Assert.Equal(originalContent, updatedContent);
    }

    [Fact]
    public void UpdateWithMultipleChangeTypesInOnePREntry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new("some-sha", "fgdfgs (#5234)")]);

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [
                         new GitHubPullRequest(
                             Merged: true,
                             """
                             Big update with many changes!

                             :cl:
                             - add: Added something new
                             - fix: Fixed a bug
                             - tweak: Tweaked some values
                             - remove: Removed old thing
                             """,
                             new GitHubUser("MultiChangeUser"),
                             new DateTimeOffset(new DateTime(2023,8,20,9,30,0), TimeSpan.Zero),
                             new GitHubPullRequestBase("master"),
                             Number: 150,
                             "https://example.com/pr/150"
                         )
                     ],
                     []
                 ));
        services.AddSingleton(ghService);


        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        var changelogPath = Path.Combine(virtualDir, "Changelog.yml");
        var updatedContent = File.ReadAllText(changelogPath);

        // Verify all change types appear
        Assert.Contains("Added something new", updatedContent);
        Assert.Contains("Fixed a bug", updatedContent);
        Assert.Contains("Tweaked some values", updatedContent);
        Assert.Contains("Removed old thing", updatedContent);
    }

    [Fact]
    public void UpdateCommand_HasRevertedPullRequests_RemovesTheirEntriesFromChangelog()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        const string lastChangeSha = "last-change-sha";
        SetupLocalRepository(services, lastChangeSha, [new("some-sha", "fgdfgs (#5234)")]);

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(lastChangeSha)
                 .Returns(new GitHubDiff(
                     [],
                     [42915, 42696]
                 ));
        services.AddSingleton(ghService);

        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);
        var updatedContent = File.ReadAllText(Path.Combine(virtualDir, "Changelog.yml"));
        Assert.DoesNotContain("Lizards can laugh again!", updatedContent);
        Assert.DoesNotContain("/pull/42696", updatedContent);
        Assert.Contains("Cyborgs can now pry unpowered doors without the need for a crowbar", updatedContent);
    }

    #endregion

    #region DumpDiff

    [Fact]
    public void DumpDiffCommand_HaveChanges_DumpsMarkdown()
    {
        // Arrange: use the full DI setup from Registry, then override test-specific parts
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        // Stub out the GitHub service
        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(Arg.Any<string>())
            .Returns(new GitHubDiff(
                [
                    new GitHubPullRequest(
                        Merged: true,
                        """
                        A PR with a cool changelog!

                        :cl:
                        - add: Dump diff entry
                        """,
                        new GitHubUser("TestUser"),
                        new DateTimeOffset(new DateTime(2023,6,1,10,0,0), TimeSpan.Zero),
                        new GitHubPullRequestBase("master"),
                        Number: 99,
                        "https://example.com/pr/99"
                    )
                ],
                []
            ));

        // Stub GetLastMergedFromRef to return a date that will trigger the diff
        services.AddSingleton(ghService);

        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act: invoke dump-diff command
        var mdPath = Path.Combine(virtualDir, "diff.md");
        var parseResult = command.Parse($"dump-diff --sha deadbeef --changelog-md-path \"{mdPath}\"");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // Assert: the markdown file was created and contains the entry
        Assert.True(File.Exists(mdPath));
        var content = File.ReadAllText(mdPath);
        Assert.Contains("Dump diff entry", content);
        Assert.Contains("TestUser", content);
    }


    [Fact]
    public void DumpDiffCommand_WithExceptCategory_ExcludesIt()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services, extraCategories: "Admin");

        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(Arg.Any<string>())
            .Returns(new GitHubDiff(
                [
                    new GitHubPullRequest(
                        Merged: true,
                        """
                        PR with main and admin changes

                        :cl:
                        - add: Main category entry
                        admin:
                        - fix: Admin category entry
                        """,
                        new GitHubUser("ExcludeTestUser"),
                        new DateTimeOffset(new DateTime(2024,5,1,12,0,0), TimeSpan.Zero),
                        new GitHubPullRequestBase("master"),
                        Number: 300,
                        "https://example.com/pr/300"
                    )
                ],
                []
            ));

        services.AddSingleton(ghService);

        var virtualDir = CopyExistingChangelogs();
        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act: dump with except-category=Admin
        var mdPath = Path.Combine(virtualDir, "diff.md");
        var parseResult = command.Parse($"dump-diff --sha deadbeef --changelog-md-path \"{mdPath}\" --except-category Admin");
        var invokeResult = parseResult.Invoke(_invocationConfiguration);

        // Assert
        Assert.Equal(0, invokeResult);

        // Assert
        Assert.True(File.Exists(mdPath));
        var content = File.ReadAllText(mdPath);
        Assert.Contains("Main category entry", content);
        Assert.DoesNotContain("Admin category entry", content);
    }

    #endregion

    #region SendWebhook

    [Fact]
    public async Task SendWebhookCommand_Success_SendsChangelogToDiscord()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        // Mock the HttpMessageHandler so the real service runs against a fake HTTP endpoint
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);

        services.RemoveAll<DiscordWebhookService>();
        services.AddSingleton(
            sp => new DiscordWebhookService(
                httpClient, 
                sp.GetRequiredService<IOptions<ChangelogToolOptions>>(), 
                sp.GetRequiredService<ILogger<DiscordWebhookService>>()
            )
        );

        // Create a temporary markdown file
        var tempPath = Path.Combine(Path.GetTempPath(), "ss14_sendwebhook_test_" + Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        Directory.CreateDirectory(tempPath);
        var mdPath = Path.Combine(tempPath, "diff.md");
        await File.WriteAllTextAsync(mdPath, "# Changelog\n\n- Some changes!");

        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"send-webhook --changelog-md-path \"{mdPath}\"");
        var exitCode = await parseResult.InvokeAsync();

        // Assert: exit code 0 and the real service actually sent an HTTP POST with the file content
        Assert.Equal(0, exitCode);
        Assert.Equal(1, mockHandler.Called);
        Assert.Equal("https://discord.com/api/webhooks/test?wait=true", mockHandler.Urls.Single());
        Assert.Equal(
            """
            {"content":"# Changelog\n\n- Some changes!\n","allowed_mentions":{"parse":[]},"flags":4}
            """,
            mockHandler.Requests.Single()
        );
    }

    [Fact]
    public async Task SendWebhookCommand_DiscordBadRequests_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterDependencies();

        OverrideOptions(services);

        // Mock the HttpMessageHandler so the real service runs against a fake HTTP endpoint
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
        var httpClient = new HttpClient(mockHandler);

        services.RemoveAll<DiscordWebhookService>();
        services.AddSingleton(
            sp => new DiscordWebhookService(
                httpClient,
                sp.GetRequiredService<IOptions<ChangelogToolOptions>>(),
                sp.GetRequiredService<ILogger<DiscordWebhookService>>()
            )
        );

        var tempPath = Path.Combine(Path.GetTempPath(), "ss14_sendwebhook_fail_test_" + Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        Directory.CreateDirectory(tempPath);
        var mdPath = Path.Combine(tempPath, "diff.md");
        await File.WriteAllTextAsync(mdPath, "# Changelog\n\n- Some changes!");

        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<RootCommand>();

        // Act
        var parseResult = command.Parse($"send-webhook --changelog-md-path \"{mdPath}\"");
        var exitCode = await parseResult.InvokeAsync();

        // Assert: exit code 1, the request was still made but the API rejected it
        Assert.Equal(1, exitCode);
        Assert.Equal(1, mockHandler.Called);
    }

    #endregion

    private string CopyExistingChangelogs()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ss14_changelog_test_" + Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        var resourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        Directory.CreateDirectory(tempPath);
        foreach (var file in Directory.GetFiles(resourceDir, "*.yml"))
        {
            File.Copy(file, Path.Combine(tempPath, Path.GetFileName(file)));
        }

        return tempPath;
    }

    private void OverrideOptions(ServiceCollection services, int? maxLogEntries = null, string? extraCategories = null)
    {
        // Route all logging from the commands and their services to the xUnit test output instead of the console.
        services.RemoveAll<ILoggerProvider>();
        services.AddSingleton<ILoggerProvider>(new TestOutputLoggerProvider(outputHelper));

        services.RemoveAll<IConfigureOptions<ChangelogToolOptions>>();
        var config = new ChangelogToolOptions
        {
            Repo = "space-wizards/SS14.ChangelogTool",
            GithubToken = "fake-token",
            ChangelogRepoPath = ".",
            MaxPullRequestEntriesInGraphQLRequest = 1,
            MaxChangelogEntries = maxLogEntries ?? 500,
            ExtraCategories = extraCategories,
            DiscordWebHook = "https://discord.com/api/webhooks/test",
            DiscordWebhookCharacterLimit = 2000,
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(config));
    }

    private static void SetupLocalRepository(ServiceCollection services, string lastChangeSha, IEnumerable<CommitBriefInfo> commits)
    {
        services.RemoveAll<ILocalGitRepository>();

        var repo = Substitute.For<ILocalGitRepository>();

        repo.GetLastCommitData(Arg.Any<string>())
            .Returns(new LastCommitData(lastChangeSha, new DateTimeOffset(2024, 4, 5, 0, 0, 0, TimeSpan.Zero)));

        repo.GetCommitsSince(lastChangeSha)
            .Returns(commits);

        services.AddSingleton(repo);
    }

    public void Dispose()
    {
        foreach (var tempPath in _tempPaths)
        {
            if (Path.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }
}