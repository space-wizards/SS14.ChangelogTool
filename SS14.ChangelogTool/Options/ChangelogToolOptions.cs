using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace SS14.ChangelogTool.Options;

/// <summary>
/// Class containing configuration. This is taken from the env variables or a .env file in the working directory
/// </summary>
public sealed class ChangelogToolOptions
{
    /// <summary>
    /// The repository to use, in format of 'owner/repo-name'.
    /// </summary>
    [Required]
    [ConfigurationKeyName("REPO")]
    public required string Repo { get; set; }

    /// <summary>
    /// The host of the repo. `codeberg.org` is the default option.
    /// Currently, this only sets the API slug on the Forgejo PR provider
    /// </summary>
    [ConfigurationKeyName("HOST")]
    public string Host { get; set; } = "codeberg.org";

    /// <summary>
    /// Allows you to switch to different PR providers.
    /// This allows GitHub, and Forgejo. It defaults to GitHub.
    /// </summary>
    [ConfigurationKeyName("PR_PROVIDER")]
    public PullRequestProvider PrProvider { get; set; } = PullRequestProvider.GitHub;

    /// <summary>
    /// The relative path to the changelog directory. Should probably be Resources/Changelog.
    /// </summary>
    [Required]
    [ConfigurationKeyName("CHANGELOG_REPO_PATH")]
    public required string ChangelogRepoPath { get; set; }

    /// <summary>
    /// The main file that will be updated when generating changelogs that are not targeting some special extra categories.
    /// This defaults to 'Changelog' which generates to Changelog.yml.
    /// Forks will typically want to change this value to match the name of their changelog file.
    /// </summary>
    [ConfigurationKeyName("PRIMARY_CHANGELOG")]
    public string PrimaryChangelog { get; set; } = "Changelog";
    
    /// <summary>
    /// The extra categories to scan. E.g. for wizden there is Admin, Maps and Rule.
    /// IF multiple needed - separate them using ','.
    /// If they are enabled, the last ran time will be affected by them as well. 
    /// </summary>
    [ConfigurationKeyName("EXTRA_CATEGORIES")]
    public string? ExtraCategories { get; set; }

    /// <summary>
    /// The github PAT to use. Should have content.read
    /// </summary>
    [Required]
    [ConfigurationKeyName("GITHUB_TOKEN")]
    public required string GithubToken { get; set; }

    /// <summary>
    /// The discord webhook to use in sending changelog diffs
    /// </summary>
    [ConfigurationKeyName("DISCORD_WEBHOOK")]
    public string? DiscordWebHook { get; set; }

    /// <summary>
    /// The maximum number of characters per Discord webhook message; used to split long changelogs.
    /// </summary>
    [ConfigurationKeyName("DISCORD_WEBHOOK_CHARACTER_LIMIT")]
    public int DiscordWebhookCharacterLimit { get; set; } = 2000;

    /// <summary>
    /// Amount of pull request entries to fetch in a single GraphQL request.
    /// </summary>
    [ConfigurationKeyName("MAX_PULL_REQUEST_ENTRIES_IN_GRAPHQL_REQUEST")]
    public int MaxPullRequestEntriesInGraphQLRequest { get; set; } = 50;

    /// <summary>
    /// Amount of commit entries to fetch in a single GraphQL request.
    /// </summary>
    [ConfigurationKeyName("MAX_COMMIT_ENTRIES_IN_GRAPHQL_REQUEST")]
    public int MaxCommitEntriesInGraphQLRequest { get; set; } = 50;

    /// <summary>
    /// Maximum number of changelog entries to keep in each YAML file; older entries are pruned.
    /// </summary>
    [ConfigurationKeyName("MAX_CHANGELOG_ENTRIES")]
    public int MaxChangelogEntries { get; set; } = 500;

    /// <summary>
    /// Maximum retries number for gh api calls when they fail or requests have to consecutively waiting due to rate-limiting.
    /// </summary>
    [ConfigurationKeyName("MAX_RETRIES_FOR_GIT_HUB_API")]
    public int MaxRetriesForGitHubApi { get; set; } = 12;

    /// <summary>
    /// Maximum wait time between attempts for gh api calls when call fails.
    /// Uses exponential backoff retries, starts with <see cref="MinWaitForGitHubApiSeconds"/>.
    /// </summary>
    [ConfigurationKeyName("MAX_WAIT_FOR_GIT_HUB_API_SECONDS")]
    public int MaxWaitForGitHubApiSeconds { get; set; } = 32;

    /// <summary>
    /// Minimum wait time between attempts for gh api calls when api call fails.
    /// Uses exponential backoff retries.
    /// </summary>
    [ConfigurationKeyName("MIN_WAIT_FOR_GIT_HUB_API_SECONDS")]
    public int MinWaitForGitHubApiSeconds { get; set; } = 2;

    /// <summary>
    /// If enabled - will check each squash commit and only add changelog
    /// from commits that were added in current <see cref="Repo"/> repository, and discard others.
    /// </summary>
    /// <remarks>
    /// This is important for forks, because forks will have all commits
    /// from any repository they get merges with, but will usually be interested in only generating their own changelog.
    /// This, however, can be problem for cases where fork is getting upstream merges, for example,
    /// not from stable branch but on daily basis from master, for example - because upstream will not be generating
    /// its own changelog but will still provide features, which will look like they are missing changelog entries.
    /// </remarks>
    [ConfigurationKeyName("IS_PROCESS_ONLY_FROM_CURRENT_REPO_ENABLED")]
    public bool IsProcessOnlyFromCurrentRepoEnabled { get; set; } = true;
}

public enum PullRequestProvider
{
    GitHub,
    Forgejo,
}