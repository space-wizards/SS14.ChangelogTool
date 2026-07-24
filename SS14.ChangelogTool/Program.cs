using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SS14.ChangelogTool;

var services = new ServiceCollection();
services.RegisterDependencies();
using var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Console logging configured");

var rootCommand = new RootCommand("Changelog generator for SS14");
var commands = serviceProvider.GetServices<Command>();
foreach (var command in commands)
{
    rootCommand.Subcommands.Add(command);
}

return rootCommand.Parse(args)
    .Invoke();