using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SS14.ChangelogTool.Options;

/// <summary>
/// Specific validation because we will get data from Env variables which will be uppercased, so default validation won't do!
/// </summary>
public sealed class ChangelogToolOptionsValidator : IValidateOptions<ChangelogToolOptions>
{
    public ValidateOptionsResult Validate(string? name, ChangelogToolOptions options)
    {
        var failures = new List<string>();

        foreach (var property in typeof(ChangelogToolOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var required = property.GetCustomAttribute<RequiredAttribute>();
            if (required is null)
                continue;

            var value = property.GetValue(options);
            if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                var configKeyName = property.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name ?? property.Name;
                failures.Add($"Configuration '{configKeyName}' is required.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
