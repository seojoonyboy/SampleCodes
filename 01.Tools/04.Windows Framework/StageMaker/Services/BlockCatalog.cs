using System.IO;
using StageMaker.Models;

namespace StageMaker.Services;

/// <summary>
/// Scans Blocks/ once and caches the resulting list for reuse by every palette slot.
/// </summary>
public static class BlockCatalog
{
    private static readonly Lazy<IReadOnlyList<BlockDefinition>> AllLazy = new(Load);

    public static IReadOnlyList<BlockDefinition> All => AllLazy.Value;

    private static List<BlockDefinition> Load()
    {
        var dir = ProjectPaths.BlocksDirectory;
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.EnumerateFiles(dir, "*.png")
            .Where(f => string.Equals(Path.GetExtension(f), ".png", StringComparison.OrdinalIgnoreCase))
            .Select(f => new BlockDefinition(Path.GetFileNameWithoutExtension(f), Path.GetFileName(f), f))
            .OrderBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
