// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace RestSiteSmithPick;

internal static class Config
{
    private const string ManifestFileName = "rest_site_smith_pick.json";
    private const string ProgressFilePrefix = "rest_site_smith_pick_progress";
    private const string ProgressFileExtension = ".json";
    private static readonly int[] DefaultCycle = { 2 };

    private static int[] _cycle = DefaultCycle;
    private static int _cycleIndex;
    private static bool _loaded;
    private static string _progressScope = "default";
    private static string? _loadedProgressScope;

    internal static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var dllDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
            if (string.IsNullOrEmpty(dllDir))
            {
                Log.Info("Could not determine DLL directory, using default upgrade_count=2");
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
            _loadedProgressScope = null;

            if (_cycle.Length > 1)
                Log.Info($"Loaded upgrade_count cycle: [{string.Join(", ", _cycle)}].");
            else
                Log.Info($"Loaded upgrade_count={_cycle[0]}.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load config. Using default upgrade_count=2", ex);
        }
    }

    public static void UseProgressScope(string scopeKey)
    {
        var scope = NormalizeProgressScope(scopeKey);
        if (string.Equals(_loadedProgressScope, scope, StringComparison.Ordinal))
            return;

        _progressScope = scope;
        _cycleIndex = 0;
        LoadProgress();
        _loadedProgressScope = scope;
    }

    public static int PeekUpgradeCount()
    {
        return _cycle[_cycleIndex % _cycle.Length];
    }

    public static void AdvanceCycle()
    {
        if (_loadedProgressScope is null)
            UseProgressScope("default");

        _cycleIndex++;
        SaveProgress();
    }

    private static string GetProgressPath()
    {
        try
        {
            var dir = OS.GetUserDataDir();
            if (string.IsNullOrEmpty(dir))
                return "";
            return Path.Combine(dir, GetProgressFileName());
        }
        catch
        {
            return "";
        }
    }

    private static string GetProgressFileName()
    {
        return $"{ProgressFilePrefix}_{_progressScope}{ProgressFileExtension}";
    }

    private static void LoadProgress()
    {
        try
        {
            var path = GetProgressPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var json = File.ReadAllText(path);
            _cycleIndex = ParseCycleIndex(json);
            Log.Info($"Loaded cycle progress: index={_cycleIndex} from {path}");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load cycle progress, starting at index 0.", ex);
        }
    }

    private static void SaveProgress()
    {
        try
        {
            var path = GetProgressPath();
            if (string.IsNullOrEmpty(path))
                return;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = $"{{\"cycle_index\":{_cycleIndex}}}";
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save cycle progress.", ex);
        }
    }

    private static int ParseCycleIndex(string json)
    {
        var key = "\"cycle_index\"";
        var idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return 0;

        var colonIdx = json.IndexOf(':', idx + key.Length);
        if (colonIdx < 0) return 0;

        for (var i = colonIdx + 1; i < json.Length; i++)
        {
            if (char.IsWhiteSpace(json[i])) continue;
            if (char.IsDigit(json[i]) || json[i] == '-')
            {
                var endIdx = i;
                while (endIdx < json.Length && char.IsDigit(json[endIdx])) endIdx++;
                if (int.TryParse(json.Substring(i, endIdx - i), out var count))
                    return Math.Max(0, count);
                break;
            }
            break;
        }

        return 0;
    }

    private static string NormalizeProgressScope(string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
            return "default";

        var builder = new StringBuilder();
        foreach (var c in scopeKey.Trim())
        {
            if (builder.Length >= 64)
                break;

            builder.Append(IsSafeFileNameChar(c) ? c : '_');
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(normalized) ? "default" : normalized;
    }

    private static bool IsSafeFileNameChar(char c)
    {
        return c is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '_' or '-';
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
