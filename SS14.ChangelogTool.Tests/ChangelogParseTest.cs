using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SS14.ChangelogTool.Models;
using SS14.ChangelogTool.Models.GitHub;
using SS14.ChangelogTool.Options;

namespace SS14.ChangelogTool.Tests;

// NUnit assertion is bugged.
[SuppressMessage("Assertion", "NUnit2022:Missing property required for constraint")]
public class ChangelogParseTest
{
    [Fact]
    public void Test()
    {
        const string text = """
                            Did stuff!

                            :cl: Ev1__l PB323
                            - add: Did the thing
                            - remove: Removed the thing
                            - fix: A
                            - bugfix: B
                            - bug: C

                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("PJB"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

        Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Ev1__l PB323", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Remove, "Removed the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Fix, "A"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Fix, "B"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Fix, "C"));
    }

    [Fact]
    public void TestWithoutName()
    {
        const string text = """

                            Did stuff!

                            :cl:
                            - add: Did the thing
                            - remove: Removed the thing

                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("Swept"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

        Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Swept", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Equal(2, entry.Changes.Count);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Remove, "Removed the thing"));
    }

    [Fact]
    public void TestMissingUserDoesNotCrash()
    {
        const string text = """
                            Did stuff!

                            :cl:
                            - add: Did the thing

                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        // GitHub returns "author: null" for PRs whose author account was deleted (User deserializes to null).
        var pr = new GitHubPullRequest(true, text, null, time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

        Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Unknown", entry.Author);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
    }

    [Fact]
    public void TestComment()
    {
        const string text = """

                            Did stuff!

                            <!-- The :cl: symbol
                            -->

                            :cl:
                            - add: Did the thing
                            - remove: Removed the thing

                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("Swept"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

                Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Swept", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Equal(2, entry.Changes.Count);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Remove, "Removed the thing"));
    }

    [Fact]
    public void TestBroke()
    {
        const string text =
            "Makes it possible to repair things with a welder.\r\n\r\n**Changelog**\r\n:cl: AJCM\r\n- add: Makes gravity generator and windows repairable with a lit welding tool \r\n\r\n";

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("AJCM-Git"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

                Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("AJCM", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Single(entry.Changes);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Makes gravity generator and windows repairable with a lit welding tool"));
    }

    [Fact]
    public void TestCategory()
    {
        const string text = """

                            Did stuff!

                            :cl:
                            ADMIN:
                            - add: Did the thing
                            - remove: Removed the thing

                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("Swept"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = [];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

                Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Swept", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Equal(2, entry.Changes.Count);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Remove, "Removed the thing"));
    }

    [Fact]
    public void TestCategoryMulti()
    {
        const string text = """

                            Did stuff!

                            :cl:
                            ADMIN:
                            - add: Did the thing
                            - remove: Removed the thing
                            MAIN:
                            - add: Did more thing
                            - remove: Removed more thing
                            ADMIN:
                            - fix: Fix the thing
                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("Swept"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = ["Admin"];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

                Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, kvp => kvp.Key == "Admin"
            && kvp.Value.Changes.SequenceEqual(new ChangeDescription[]
            {
                new(ChangeType.Add, "Did the thing"),
                new(ChangeType.Remove, "Removed the thing"),
                new(ChangeType.Fix, "Fix the thing"),
            }));
        Assert.Contains(parsed, kvp => kvp.Key == Constants.MainCategory
            && kvp.Value.Changes.SequenceEqual(new ChangeDescription[]
            {
                new(ChangeType.Add, "Did more thing"),
                new(ChangeType.Remove, "Removed more thing"),
            }));
    }

    [Fact]
    public void TestCategoryInvalid()
    {
        const string text = """

                            Did stuff!

                            :cl:
                            - add: Did the thing
                            - remove: Removed the thing
                            NOTACATEGORY:
                            - add: WOW
                            """;

        var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
        var pr = new GitHubPullRequest(true, text, new GitHubUser("Swept"), time, new GitHubPullRequestBase("master"), 123,
            "https://www.example.com");
        IReadOnlyCollection<string> extraCategories = ["Admin"];
        var parsed = Services.ChangelogParserService.ParsePrBody(pr, extraCategories);

                Assert.NotNull(parsed);
        var entry = parsed.Single().Value;
        Assert.Equal("Swept", entry.Author);
        Assert.Equal(time.ToString("O"), entry.Time);
        Assert.Equal("https://www.example.com", entry.Url);
        Assert.Equal(3, entry.Changes.Count);
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "Did the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Remove, "Removed the thing"));
        Assert.Contains(entry.Changes, x => x == new ChangeDescription(ChangeType.Add, "WOW"));
    }

    [Fact]
    public void OptionsValidationFailsWhenRequiredValuesMissing()
    {
        var services = new ServiceCollection();
        services.AddOptions<ChangelogToolOptions>()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ChangelogToolOptions>, ChangelogToolOptionsValidator>();

        var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ChangelogToolOptions>>().Value);

        Assert.Equal(3, exception.Failures.Count());
        Assert.Contains("Configuration 'REPO' is required.", exception.Failures);
        Assert.Contains("Configuration 'CHANGELOG_REPO_PATH' is required.", exception.Failures);
        Assert.Contains("Configuration 'GITHUB_TOKEN' is required.", exception.Failures);
    }
}