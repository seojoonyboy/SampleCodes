namespace StageMaker.Models;

/// <summary>
/// A single selectable block resource found under the Blocks/ folder.
/// Id is the block "type" number (e.g. "101"), matching the "type" field
/// used in exported Stages/*.json files.
/// </summary>
public sealed record BlockDefinition(string Id, string FileName, string FullPath);
