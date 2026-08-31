using System.IO;

namespace StageMaker.Services;

/// <summary>
/// Locates the repo-level Blocks/ and Stages/ folders relative to the running app,
/// so the tool always reads/writes the same folders regardless of build configuration.
/// </summary>
public static class ProjectPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    public static string RootDirectory => RootLazy.Value;
    public static string BlocksDirectory => Path.Combine(RootDirectory, "Blocks");
    public static string StagesDirectory => Path.Combine(RootDirectory, "Stages");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Blocks")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Stages")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fallback if Blocks/Stages weren't found walking up (e.g. moved build output):
        // repo root is 5 levels above src/StageMaker/bin/<Config>/<TFM>/.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
