using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using SS14.ChangelogTool.Options;

namespace SS14.ChangelogTool.Services;

/// <summary>
/// Service for working with posting changelog onto discord by discord webhooks.
/// </summary>
public class DiscordWebhookService(HttpClient client, IFileSystem fileSystem, IOptions<ChangelogToolOptions> options, ILogger<DiscordWebhookService> logger)
{
    private readonly ChangelogToolOptions _options = options.Value;

    /// <summary>
    /// Sends changelog entries, located in files from provided path to discord.
    /// </summary>
    public async Task<bool> SendDiffInParts(string changelogMarkdownPath)
    {
        var discordHook = _options.DiscordWebHook;
        if (discordHook is null)
            throw new Exception("Discord webhook is not set in environment or could not be read from .env in working dir");

        using var stream = fileSystem.File.OpenRead(changelogMarkdownPath);
        using var contentStreamReader = new StreamReader(stream);

        var characterLimit = _options.DiscordWebhookCharacterLimit;
        var sb = new StringBuilder(characterLimit);

        var nextLine = await contentStreamReader.ReadLineAsync();
        while (nextLine is not null)
        {
            sb.Append(nextLine + "\n");

            nextLine = await contentStreamReader.ReadLineAsync();

            if (nextLine is null)
                break;

            // if we are not going to exceed the discord limit with the next message, continue adding lines
            if (sb.Length + nextLine.Length < characterLimit)
                continue;

            // otherwise send the part
            if (! await TrySendPart(discordHook, sb.ToString()))
                return false;

            sb.Clear();
        }

        // send the leftover part after breaking out of the while loop
        if (sb.Length > 0)
            return await TrySendPart(discordHook, sb.ToString());

        return true;
    }

    private async Task<bool> TrySendPart(string webhookUrl, string contentPart)
    {
        // specific body for the webhook request. "content" contains the actual content of the discord message
        var discordWebhookBody = new Dictionary<string, object>
        {
            { "content", contentPart },
            // disallow mentions
            { "allowed_mentions", new Dictionary<string, List<string>> { { "parse", [] } } },
            // disable embeds
            { "flags", 1 << 2 },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl + "?wait=true");
        request.Content = JsonContent.Create(discordWebhookBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    logger.LogError("Bad request response received, cancelling: {Response}", await response.Content.ReadAsStringAsync());
                    return false;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogWarning("Received 429 TooManyRequests after retries.");
                    return false;
                }

                logger.LogError("Received unexpected response status code ({StatusCode}), cancelling: {Response}", response.StatusCode, await response.Content.ReadAsStringAsync());
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception when sending Discord webhook");
            return false;
        }
    }
}