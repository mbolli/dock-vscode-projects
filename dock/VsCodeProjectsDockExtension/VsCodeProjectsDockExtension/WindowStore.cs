// Reads the shared state directory the companion writes to (see ../../contract/).
// Liveness is file mtime, per the contract: a file untouched longer than the reap
// window is a dead window (crash / force-quit / sleep run no clean-exit delete).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VsCodeProjectsDockExtension;

internal readonly record struct WindowState(
    string WindowId, string FolderUri, string RemoteKind, string DisplayName);

internal static class WindowStore
{
    // The companion's default state directory, used when the setting is left empty.
    internal static string DefaultSharedDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VsCodeProjectsDock", "windows");

    // dir + timeout come from settings (see SettingsManager). A file untouched longer
    // than the timeout is a dead window: skipped and deleted.
    internal static List<WindowState> ReadLive(string dir, int timeoutSeconds)
    {
        var windows = new List<WindowState>();
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return windows;
        }

        var reapAfter = TimeSpan.FromSeconds(timeoutSeconds);
        var now = DateTime.UtcNow;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                // Reap dead windows: skip stale files and delete them so a crashed
                // window doesn't linger in the dir. A live-but-laggy window simply
                // rewrites its file on the next heartbeat.
                if (now - File.GetLastWriteTimeUtc(file) > reapAfter)
                {
                    TryDelete(file);
                    continue;
                }

                using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
                var root = doc.RootElement;
                var windowId = GetString(root, "windowId");
                var folderUri = GetString(root, "folderUri");
                var displayName = GetString(root, "displayName");
                // folderUri is the match key, windowId the dedup/Id key. A file missing
                // either is a torn write or a foreign file — skip it, don't fail.
                if (string.IsNullOrEmpty(windowId) || string.IsNullOrEmpty(folderUri))
                {
                    continue;
                }
                windows.Add(new WindowState(
                    windowId,
                    folderUri,
                    GetString(root, "remoteKind") ?? "local",
                    string.IsNullOrEmpty(displayName) ? folderUri : displayName));
            }
            catch (JsonException)
            {
                // Truncated/foreign JSON — ignore this file.
            }
            catch (IOException)
            {
                // Locked or vanished between enumerate and read — skip this tick.
            }
        }

        return windows;
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
