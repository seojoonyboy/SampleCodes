using System.Text.Json;
using System.Text.Json.Serialization;
using StageMaker.Models;

namespace StageMaker.Services;

/// <summary>
/// Writes each "cells" entry as a single compact line (e.g. {"block":{"type":"101"}}),
/// matching existing Stages/*.json, even though the surrounding document is indented.
/// </summary>
public sealed class CellExportJsonConverter : JsonConverter<CellExport>
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override CellExport? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<CellExport>(ref reader, CompactOptions);

    public override void Write(Utf8JsonWriter writer, CellExport value, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, CompactOptions);

        // WriteRawValue doesn't participate in the writer's automatic indentation,
        // so the leading newline + indent has to be added by hand here. Hardcoded to
        // System.Text.Json's default indent (2 spaces) since JsonWriterOptions only
        // exposes IndentCharacter/IndentSize starting on .NET 9, not net8.0-windows.
        if (writer.Options.Indented)
        {
            var indent = new string(' ', 2 * writer.CurrentDepth);
            json = "\n" + indent + json;
        }

        writer.WriteRawValue(json, skipInputValidation: true);
    }
}
