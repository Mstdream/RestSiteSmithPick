// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using System.IO;

namespace RestSiteSmithPick;

internal static class Config
{
    private const string ManifestFileName = "rest_site_smith_pick.json";
    private const int DefaultUpgradeCount = 2;

    public static int UpgradeCount { get; private set; } = DefaultUpgradeCount;

    internal static void Load()
    {
        try
        {
            var dllDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
            if (string.IsNullOrEmpty(dllDir))
            {
                Log.Info($"Could not determine DLL directory, using default upgrade_count={DefaultUpgradeCount}");
                return;
            }

            var manifestPath = Path.Combine(dllDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Log.Info($"Manifest not found at {manifestPath}, using default upgrade_count={DefaultUpgradeCount}");
                return;
            }

            var json = File.ReadAllText(manifestPath);
            UpgradeCount = ParseUpgradeCount(json);
            Log.Info($"Loaded upgrade_count={UpgradeCount} from manifest.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load config. Using default upgrade_count={DefaultUpgradeCount}", ex);
        }
    }

    private static int ParseUpgradeCount(string json)
    {
        // Manual parsing to avoid System.Text.Json dependency
        var key = "\"upgrade_count\"";
        var idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return DefaultUpgradeCount;

        var colonIdx = json.IndexOf(':', idx + key.Length);
        if (colonIdx < 0) return DefaultUpgradeCount;

        // Skip whitespace and find the number
        for (var i = colonIdx + 1; i < json.Length; i++)
        {
            if (char.IsWhiteSpace(json[i])) continue;
            if (char.IsDigit(json[i]) || json[i] == '-')
            {
                var endIdx = i;
                while (endIdx < json.Length && char.IsDigit(json[endIdx])) endIdx++;
                if (int.TryParse(json.Substring(i, endIdx - i), out var count))
                {
                    return Math.Max(1, count);
                }
                break;
            }
            break;
        }

        return DefaultUpgradeCount;
    }
}
