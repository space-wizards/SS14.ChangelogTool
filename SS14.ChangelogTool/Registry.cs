using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SS14.ChangelogTool.Clients;
using SS14.ChangelogTool.Commands;
using SS14.ChangelogTool.Options;
using SS14.ChangelogTool.Services;
using System.CommandLine;
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
        services.AddHttpClient<DiscordWebhookService>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );

        services.AddHttpClient<GraphQLHttpClient>(
            (sp, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                var options = sp.GetRequiredService<IOptions<ChangelogConfigOptions>>().Value;
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.GithubToken}");
            }
        );

        services.AddSingleton<IGraphQLClient>(sp =>
        {
            var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient(nameof(GraphQLHttpClient));
            return new GraphQLHttpClient(GithubPullRequestClient.GithubGraphQLApiBase, new SystemTextJsonSerializer(), client);
        });

        services.AddSingleton<ChangelogGeneratorService>();

        services.AddSingleton<Command, UpdateCommand>();
        services.AddSingleton<Command, DumpDiffCommand>();
        services.AddSingleton<Command, SendWebhookCommand>();

        services.AddSingleton<RootCommand>(sp =>
        {
            var rootCommand = new RootCommand("Changelog generator for SS14");
            var commands = sp.GetServices<Command>();
            foreach (var command in commands)
            {
                rootCommand.Subcommands.Add(command);
            }

            return rootCommand;
        });

        services.AddHttpClient<IGitHubPullRequestService>();
        return services;
    }
}