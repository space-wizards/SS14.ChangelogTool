using System.CommandLine;
using SS14.ChangelogTool.Services;

namespace SS14.ChangelogTool.Commands;

public sealed class DumpDiffCommand : Command
{
    public DumpDiffCommand(
		IGitHubPullRequestService gitHubService,
		IChangelogFileManager changelogFileManager,
		ChangelogGeneratorService changelogGeneratorService
	) : base("dump-diff", "Dumps a diff to a markdown file, for later sending to discord or hosting on CDN")
	{
        var sinceRefShaOption = new Option<string>("--sha", "-s")
		{
			Description = "Specific ref sha to compare changes to. Good chance this should be the github.event.pull_request.base.sha workflow env",
			Required = true,
		};

		var changelogMarkdownPathOption = new Option<string>("--changelog-md-path", "-c")
		{
			Description = "Path where the changelog markdown file is located. This will be sent to the discord webhook.",
			Required = true,
		};

        var exceptCategoryOption = new Option<string>("--except-category", "-e")
        {
            Description = $"Except specified changelog category entries from output. Uses {Constants.MainCategory} value by default.",
            Required = false
        };

		Options.Add(sinceRefShaOption);
		Options.Add(changelogMarkdownPathOption);
		Options.Add(exceptCategoryOption);

		SetAction(async parseResult =>
            {
                var sha = parseResult.GetValue(sinceRefShaOption)!;
                var changelogMarkdownPath = parseResult.GetValue(changelogMarkdownPathOption)!;
                var exceptCategory = parseResult.GetValue(exceptCategoryOption)!;
                return await changelogGeneratorService.TryGenerate(
                    extraCategories => gitHubService.GetNewestChangelogEntryMergeDateByRef(sha, extraCategories),
                    changelogs => changelogFileManager.DumpChangelogToMarkdown(changelogMarkdownPath, changelogs, exceptCategory)
                ) ? 0 : 1;
            }
        );
	}
}
