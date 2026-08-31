namespace StageMaker.Models;

/// <summary>
/// Root object written to Stages/*.json on Export.
/// Only Header/Cells are populated for real right now; Clears/Components/
/// Genesises/Zones are kept as empty placeholders so the file shape matches
/// existing Stages/*.json and can be wired up from the Export button later.
/// </summary>
public sealed class StageExportModel
{
    public StageHeaderExport Header { get; set; } = new();
    public List<object> Clears { get; set; } = new();
    public Dictionary<string, object> Components { get; set; } = new();
    public List<object> Genesises { get; set; } = new();
    public List<object> Zones { get; set; } = new();
    public List<List<CellExport?>> Cells { get; set; } = new();
}

public sealed class StageHeaderExport
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CollectionNo { get; set; }
    public int StageNo { get; set; }
    public int RowCount { get; set; }
    public int ColCount { get; set; }
    public string TotalTurn { get; set; } = "0";
    public string SkillFeverMax { get; set; } = "0";
    public bool IsAutoOneZone { get; set; } = true;
    public string Version { get; set; } = "1.0.0";
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>One "cells" grid entry. Null means no block has been placed there yet.</summary>
public sealed class CellExport
{
    public BlockExport Block { get; set; } = new();
}

public sealed class BlockExport
{
    /// <summary>The selected block's image name (e.g. "101"), not yet mapped to a real game type code.</summary>
    public string Type { get; set; } = string.Empty;
}
