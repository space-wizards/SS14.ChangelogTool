using YamlDotNet.Serialization;

namespace SS14.ChangelogTool.Models;

public sealed record ChangeDescription
{
    public ChangeDescription(ChangeType type, string message)
    {
        Type = type;
        Message = message;
    }

    public ChangeDescription()
    {
    }

    [YamlMember(Alias = "type")] public ChangeType Type { get; set; }

    [YamlMember(Alias = "message")] public string Message { get; set; }
}