// Dock-owned favourites — projects pinned to the dock so they show (and can be
// launched) even when no window is open. NOT part of the companion contract: the
// companion never reads this file. Stored next to the windows/ state dir.
//
// favourites.json: { "schemaVersion": 1, "favourites": [ { label, launchTarget, matchKey } ] }
//   label       — button text
//   launchTarget — passed verbatim to Code.exe --folder-uri (the folderUri)
//   matchKey     — folderUri used to dedup against live windows (defaults to launchTarget)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VsCodeProjectsDockExtension;

internal readonly record struct Favourite(string Label, string LaunchTarget, string MatchKey);

internal static class FavouritesStore
{
    private const int SchemaVersion = 1;

    private static string FilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VsCodeProjectsDock", "favourites.json");

    internal static List<Favourite> Read()
    {
        var list = new List<Favourite>();
        var path = FilePath();
        if (!File.Exists(path))
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!doc.RootElement.TryGetProperty("favourites", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var e in arr.EnumerateArray())
            {
                var launchTarget = GetString(e, "launchTarget");
                if (string.IsNullOrEmpty(launchTarget))
                {
                    continue;
                }
                var label = GetString(e, "label");
                var matchKey = GetString(e, "matchKey");
                list.Add(new Favourite(
                    string.IsNullOrEmpty(label) ? launchTarget : label,
                    launchTarget,
                    string.IsNullOrEmpty(matchKey) ? launchTarget : matchKey));
            }
        }
        catch (JsonException) { }
        catch (IOException) { }

        return list;
    }

    internal static void Add(Favourite fav)
    {
        var list = Read();
        if (list.Exists(f => f.MatchKey == fav.MatchKey))
        {
            return; // already pinned — pinning is idempotent
        }
        list.Add(fav);
        Write(list);
    }

    internal static void Remove(string matchKey)
    {
        var list = Read();
        if (list.RemoveAll(f => f.MatchKey == matchKey) > 0)
        {
            Write(list);
        }
    }

    private static void Write(List<Favourite> favs)
    {
        var path = FilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("schemaVersion", SchemaVersion);
                w.WriteStartArray("favourites");
                foreach (var f in favs)
                {
                    w.WriteStartObject();
                    w.WriteString("label", f.Label);
                    w.WriteString("launchTarget", f.LaunchTarget);
                    w.WriteString("matchKey", f.MatchKey);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }

            // Temp-then-move so a concurrent read never sees a torn file.
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, stream.ToArray());
            File.Move(tmp, path, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
