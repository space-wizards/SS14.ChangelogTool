using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Models;

public sealed class ChangelogEntry
{
    /// <summary>
    /// Unique id in list of changelog entries.
    /// Is based on simple incrementation of number from last id in file, is not synced to some database.
    /// </summary>
    [YamlMember(Alias = "id", Order = 2)]
    public int Id { get; set; }

    /// <summary>
    /// PR id in gh.
    /// </summary>
    [YamlIgnore]
    public required int Number { get; init; }

    /// <summary>
    /// Url of PR.
    /// </summary>
    [YamlMember(Alias = "url", Order = 4)]
    public required string Url { get; init; }

    /// <summary>
    /// Username of change author.
    /// </summary>
    [YamlMember(Alias = "author", Order = 0)]
    public required string Author { get; set; }

    /// <summary>
    /// Merge time for PR.
    /// </summary>
    [YamlMember(Alias = "time", Order = 3, ScalarStyle = ScalarStyle.SingleQuoted)]
    public required string? Time { get; set; }

    /// <summary>
    /// List of changes done.
    /// </summary>
    [YamlMember(Alias = "changes", Order = 1)]
    public required List<ChangeDescription> Changes { get; set; }
}