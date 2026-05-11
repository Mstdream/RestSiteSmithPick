// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using System.Collections.Generic;
using System.IO;

namespace RestSiteSmithPick;

internal static class Config
{
    private const string ManifestFileName = "rest_site_smith_pick.json";
    private static readonly int[] DefaultCycle = { 2 };

    private static int[] _cycle = DefaultCycle;
    private static int _cycleIndex;

    internal static void Load()
    {
        try
        {
            var dllDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
            if (string.IsNullOrEmpty(dllDir))
            {
                Log.Info($"Could not determine DLL directory, using default upgrade_count=2");
                return;
            }

            var manifestPath = Path.Combine(dllDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Log.Info($"Manifest not found at {manifestPath}, using default upgrade_count=2");
                return;
            }

            var json = File.ReadAllText(manifestPath);
            _cycle = ParseUpgradeCount(json);
            _cycleIndex = 0;
            if (_cycle.Length > 1)
                Log.Info($"Loaded upgrade_count cycle: [{string.Join(", ", _cycle)}].");
            else
                Log.Info($"Loaded upgrade_count={_cycle[0]} from manifest.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load config. Using default upgrade_count=2", ex);
        }
    }

    /// <summary>Peek at the upcoming count without advancing the cycle.</summary>
    public static int PeekUpgradeCount()
    {
        return _cycle[_cycleIndex % _cycle.Length];
    }

    /// <summary>Get the count for this smith and advance the cycle.</summary>
    public static int NextUpgradeCount()
    {
        var value = _cycle[_cycleIndex % _cycle.Length];
        _cycleIndex++;
        return value;
    }

    private static int[] ParseUpgradeCount(string json)
    {
        var key = "\"upgrade_count\"";
        var idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return DefaultCycle;

        var colonIdx = json.IndexOf(':', idx + key.Length);
        if (colonIdx < 0) return DefaultCycle;

        for (var i = colonIdx + 1; i < json.Length; i++)
        {
            if (char.IsWhiteSpace(json[i])) continue;

            if (json[i] == '[')
                return ParseCycleArray(json, i + 1);

            if (char.IsDigit(json[i]) || json[i] == '-')
            {
                var endIdx = i;
                while (endIdx < json.Length && char.IsDigit(json[endIdx])) endIdx++;
                if (int.TryParse(json.Substring(i, endIdx - i), out var count))
                    return new[] { Math.Max(1, count) };
                break;
            }
            break;
        }

        return DefaultCycle;
    }

    private static int[] ParseCycleArray(string json, int startIdx)
    {
        var values = new List<int>();
        for (var i = startIdx; i < json.Length; i++)
        {
            if (char.IsWhiteSpace(json[i]) || json[i] == ',') continue;
            if (json[i] == ']') break;

            if (char.IsDigit(json[i]))
            {
                var endIdx = i;
                while (endIdx < json.Length && char.IsDigit(json[endIdx])) endIdx++;
                if (int.TryParse(json.Substring(i, endIdx - i), out var count))
                    values.Add(Math.Max(1, count));
                i = endIdx - 1;
            }
        }

        return values.Count > 0 ? values.ToArray() : DefaultCycle;
    }
}
