using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SS14.ChangelogTool.Commands;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;
using System.Net;

namespace SS14.ChangelogTool;

public static class Registry
{
    public static IServiceCollection RegisterDependencies(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        services.AddOptions<ChangelogConfigOptions>()
            .Bind(configuration)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ChangelogConfigOptions>, ChangelogConfigOptionsValidator>();

        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<IChangelogFileManager, ChangelogFileManager>();
            services.AddSingleton<IPullRequestParserService, ChangelogParserService>();
            services.AddSingleton<IGitHubPullRequestService, GitHubPullRequestService>();
        services.AddSingleton<System.IO.Abstractions.IFileSystem>(sp => new System.IO.Abstractions.FileSystem());
        // Register typed HttpClient for DiscordWebhook with a retry policy
        services.AddHttpClient<DiscordWebhookService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        services.AddSingleton<ChangelogGeneratorService>();

        services.AddSingleton<UpdateCommand>();
        services.AddSingleton<DumpDiffCommand>();
        services.AddSingleton<SendWebhookCommand>();

        services.AddHttpClient<IGitHubPullRequestService>();
        return services;
    }
}