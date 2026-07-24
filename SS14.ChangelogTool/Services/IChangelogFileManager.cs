using SS14.ChangelogTool.Models;

namespace SS14.ChangelogTool.Services;

/// <summary>
/// Writes down changelog data into files.
/// </summary>
public interface IChangelogFileManager
{
    /// <summary>
    /// Saves list of changes into file on path.
    /// </summary>
    /// <param name="saveTo">Path for file to which changelog entries should be saved.</param>
    /// <param name="changelogParts">Changelog entries grouped by changelog category.</param>
    /// <param name="exceptCategory">Category to be excepted from saving.</param>
    void DumpChangelogToMarkdown(string saveTo, Dictionary<string, List<ChangelogEntry>> changelogParts,
        string exceptCategory);

    /// <summary>
    /// Updates existing changelog files in provided directory.
    /// </summary>
    /// <param name="changelogParts">Changelog entries grouped by changelog category.</param>
    /// <param name="changelogDir">Directory, in which changelog files are.</param>
    void UpdateChangelogs(Dictionary<string, List<ChangelogEntry>> changelogParts, string changelogDir);

    /// <summary>
    /// Get newest changelog entry by PR merge datetime from changelog files in directory.
    /// </summary>
    /// <param name="changelogDir">Directory, files from which should be checked.</param>
    /// <param name="extraCategories">Extra changelog files that have to be checked.</param>
    DateTimeOffset GetLastMergedTimeFromChangelogs(string changelogDir, IReadOnlyCollection<string>? extraCategories = null);

}
