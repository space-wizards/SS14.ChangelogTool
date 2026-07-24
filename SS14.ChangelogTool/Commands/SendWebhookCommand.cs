using SS14.ChangelogTool.Services;
using System.CommandLine;

namespace SS14.ChangelogTool.Commands;

public sealed class SendWebhookCommand : Command
{
    public SendWebhookCommand(DiscordWebhookService discordWebhook)
        : base("send-webhook", "Send changelog markdown file to a discord webhook")
    {
        var changelogMarkdownPathOption = new Option<string>("--changelog-md-path", "-c")
        {
            Description = "Path where the changelog markdown file is located. This will be sent to the discord webhook.",
            Required = true,
        };

        Options.Add(changelogMarkdownPathOption);

        SetAction(async parseResult =>
            await discordWebhook.SendDiffInParts(parseResult.GetValue(changelogMarkdownPathOption)!)
                ? 0
                : 1
        );
    }
}
