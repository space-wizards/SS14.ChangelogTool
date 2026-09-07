namespace SS14.ChangelogTool.Utils;

public class GitRepositoryUtils
{
    public static (string repo, string owner) ExtractParts(string repo)
    {
        var parts = repo.Split('/', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"Attempted to split repo name {repo} into repository name and owner parts, "
                + $"but splitting by '/' resulted in {parts.Length} parts!"
            );
        }

        return (parts[0], parts[1]);
    }
}