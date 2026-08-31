using System.IO;
using System.Text.Json;
using StageMaker.Controls;
using StageMaker.Models;

namespace StageMaker.Services;

/// <summary>
/// Builds a Stages/*.json-shaped file from the current grid. For now only
/// header.rowCount/colCount and cells reflect real state; the rest are
/// placeholders to be wired up through the Export button later.
/// </summary>
public static class StageExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new CellExportJsonConverter() }
    };

    public static StageExportModel BuildExportModel(StageCell[,] cells, string stageName, IReadOnlyList<BlockDefinition> randomFillPool)
    {
        int rowCount = cells.GetLength(0);
        int colCount = cells.GetLength(1);

        var model = new StageExportModel();
        model.Header.Name = stageName;
        model.Header.RowCount = rowCount;
        model.Header.ColCount = colCount;
        model.Header.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        for (int row = 0; row < rowCount; row++)
        {
            var rowCells = new List<CellExport?>(colCount);
            for (int col = 0; col < colCount; col++)
            {
                var block = cells[row, col].SelectedBlock ?? PickRandomFillBlock(randomFillPool);
                rowCells.Add(block is null ? null : new CellExport { Block = new BlockExport { Type = block.Id } });
            }

            model.Cells.Add(rowCells);
        }

        return model;
    }

    public static void Export(string filePath, StageCell[,] cells, IReadOnlyList<BlockDefinition> randomFillPool)
    {
        var stageName = Path.GetFileNameWithoutExtension(filePath);
        var model = BuildExportModel(cells, stageName, randomFillPool);
        var json = JsonSerializer.Serialize(model, SerializerOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>Picks a random block from the pool for a cell that was left empty. Null if the pool is empty.</summary>
    private static BlockDefinition? PickRandomFillBlock(IReadOnlyList<BlockDefinition> pool)
        => pool.Count == 0 ? null : pool[Random.Shared.Next(pool.Count)];
}
