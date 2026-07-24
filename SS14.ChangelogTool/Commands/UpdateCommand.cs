using System.CommandLine;
using SS14.ChangelogTool.Services;

namespace SS14.ChangelogTool.Commands;

public sealed class UpdateCommand : Command
{
    public UpdateCommand(ChangelogGeneratorService changelogGenerator, IChangelogFileManager changelogFileManager)
        : base("update", "Updates the changelog.yml files in resources")
    {
        var changelogDirOption = new Option<string>("--changelog-dir", "-d")
        {
            Description = "Path to the changelog directory",
            Required = true,
        };

        Options.Add(changelogDirOption);

        SetAction(async parseResult =>
        {
            var changeLogDir = parseResult.GetValue(changelogDirOption)!;
            return await changelogGenerator.TryGenerate(
                extraCategories => changelogFileManager.GetLastMergedTimeFromChangelogs(changeLogDir, extraCategories),
                changelogs => changelogFileManager.UpdateChangelogs(changelogs, changeLogDir)
            ) ? 0 : 1;
        });
    }
}
