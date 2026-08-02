using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace SS14.ChangelogTool.Options;

/// <summary>
/// Class containing configuration. This is taken from the env variables or a .env file in the working directory
/// </summary>
public sealed class ChangelogToolOptions
{
    /// <summary>
    /// The repository to use
    /// </summary>
    [Required]
    [ConfigurationKeyName("REPO")]
    public required string Repo { get; set; }

    /// <summary>
    /// The branch to use as a base when gathering PRs. should probably be master or stable
    /// </summary>
    [Required]
    [ConfigurationKeyName("BRANCH")]
    public required string Branch { get; set; }

    /// <summary>
    /// the relative path to the changelog directory. should probably be Resources/Changelog
    /// </summary>
    [Required]
    [ConfigurationKeyName("CHANGELOG_REPO_PATH")]
    public required string ChangelogRepoPath { get; set; }

    /// <summary>
    /// The extra categories to scan. E.g. for wizden there is Admin, Maps and Rule.
    /// IF multiple needed - separate them using ','.
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
    /// Maximum number of pages to go through in the graphQL. if you exceed this it means you have not updated the
    /// changelog in months.
    /// </summary>
    [ConfigurationKeyName("MAX_GRAPQHL_PAGES")]
    public int MaxPages { get; set; } = 50;

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
}