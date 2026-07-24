using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Models;

/// <summary>
/// Top level container inside changelog file
/// </summary>
public sealed class ChangelogContainer
{
    /// <summary>
    /// List of changelog entries.
    /// </summary>
    [YamlMember(Alias = "Entries", Order = 1)]
    public List<ChangelogEntry> Entries { get; set; }

    /// <summary>
    /// Marker, if changelog should be presented only for admins.
    /// </summary>
    [YamlMember(Alias = "AdminOnly", Order = 0)]
    public bool AdminOnly { get; set; }
    
    /// <summary>
    /// Name for non-default changelog.
    /// </summary>
    [YamlMember(Alias = "Name", Order = 2)]
    public string? Name { get; set; }

    /// <summary>
    /// Order in list of changelogs. Null is basically 0.
    /// </summary>
    [YamlMember(Alias = "Order", Order = 3)]
    public int? Order { get; set; }
}