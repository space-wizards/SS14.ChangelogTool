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
using System.Net.Http.Headers;
using SS14.ChangelogTool.LocalGit;

namespace SS14.ChangelogTool;

public static class Registry
{
    public static IServiceCollection RegisterDependencies(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        services.AddOptions<ChangelogToolOptions>()
            .Bind(configuration)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ChangelogToolOptions>, ChangelogToolOptionsValidator>();
        
        var options = configuration.Get<ChangelogToolOptions>();

        if (options is null)
            throw new KeyNotFoundException();

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
        services.AddSingleton<IPullRequestService, GenericPullRequestService>();
        services.AddSingleton<ILocalGitRepository, LocalGitRepository>();
        
        # region Pull Request Clients
        
        switch (options.PrProvider)
        {
            case PullRequestProvider.GitHub:
                services.AddSingleton<INetworkGitRepositoryClient, GithubGraphQLClient>();
                break;
            case PullRequestProvider.Forgejo:
                services.AddSingleton<INetworkGitRepositoryClient, ForgejoGitRepositoryClient>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        # endregion

        #region clients of different flavours

        // handlers must not be reused
        services.AddTransient<RetryHandler>();

        // Register typed HttpClient for DiscordWebhook with a retry policy
        services.AddHttpClient<DiscordWebhookService>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );

        services.AddHttpClient<GraphQLHttpClient>((sp, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                var options = sp.GetRequiredService<IOptions<ChangelogToolOptions>>().Value;
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.GithubToken}");
            }
        ).AddGitHubRetryHandler();

        services.AddSingleton<IGraphQLClient>(sp =>
        {
            var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient(nameof(GraphQLHttpClient));
            return new GraphQLHttpClient(GithubGraphQLClient.GithubGraphQLApiBase, new SystemTextJsonSerializer(), client);
        });

        #endregion

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

        return services;
    }

    public static void AddGitHubRetryHandler(this IHttpClientBuilder httpClientBuilder)
    {
        httpClientBuilder.AddHttpMessageHandler<RetryHandler>();
    }
}