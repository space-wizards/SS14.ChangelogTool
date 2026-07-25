using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SS14.ChangelogTool.Commands;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;
using NSubstitute;

namespace SS14.ChangelogTool.Tests;

public class EndToEndPipelineTest
{
    [Fact]
    public void UpdateCommandWritesNewEntryToChangelog()
    {
        // Arrange: use the full DI setup from Registry, then override test-specific parts
        var services = new ServiceCollection();
        services.RegisterDependencies();

        // Override options: provide test values instead of env-based ones
        services.RemoveAll<Microsoft.Extensions.Options.IConfigureOptions<ChangelogConfigOptions>>();
        var config = new ChangelogConfigOptions
        {
            Repo = "space-wizards/SS14.ChangelogTool",
            Branch = "master",
            GithubToken = "fake-token",
            ChangelogRepoPath = ".",
            MaxPages = 1
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(config));

        // Stub out the GitHub service so it doesn't try to make real HTTP calls
        services.RemoveAll<IGitHubPullRequestService>();
        var ghService = Substitute.For<IGitHubPullRequestService>();
        ghService.GetDiff(Arg.Any<DateTimeOffset>())
            .Returns([
                new GitHubPullRequest(
                    Merged: true,
                    """
                    Adds the cool feature!

                    :cl:
                    - add: Integration test feature
                    """,
                    new GitHubUser("TestUser"),
                    new DateTimeOffset(new DateTime(2022,12,5,12,3,5)),
                    new GitHubPullRequestBase("master"),
                    Number: 42,
                    "https://example.com/pr/42"
                )
            ]);
        services.AddSingleton(ghService);

        // Use temp directory with resource files for this integration-style test
        var virtualDir = CopyExistingChangelogs();

        var sp = services.BuildServiceProvider();
        var command = sp.GetRequiredService<UpdateCommand>();

        // Act: invoke UpdateCommand just like the real program does
        var parseResult = command.Parse($"update --changelog-dir \"{virtualDir}\"");
        parseResult.Invoke();

        // Assert: the Changelog.yml now contains our integration test change
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
                                       time: '2022-12-05T12:03:05.0000000+03:00'
                                       url: https://example.com/pr/42
                                     
                                     """;
        Assert.EndsWith(expectedEntry, updatedContent);

        // Cleanup
        try { Directory.Delete(virtualDir, true); } catch { }
    }

    private static string CopyExistingChangelogs()
    {
        var virtualDir = Path.Combine(Path.GetTempPath(), "ss14_changelog_test_" + Guid.NewGuid().ToString("N"));
        var resourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        Directory.CreateDirectory(virtualDir);
        foreach (var file in Directory.GetFiles(resourceDir, "*.yml"))
        {
            File.Copy(file, Path.Combine(virtualDir, Path.GetFileName(file)));
        }

        return virtualDir;
    }
}
