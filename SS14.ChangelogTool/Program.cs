using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SS14.ChangelogTool;

var services = new ServiceCollection();
services.RegisterDependencies();
using var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Console logging configured");

var rootCommand = serviceProvider.GetRequiredService<RootCommand>();
return rootCommand.Parse(args)
    .Invoke();