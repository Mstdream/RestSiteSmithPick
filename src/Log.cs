// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using Godot;

namespace RestSiteSmithPick;

internal static class Log
{
    private const string Prefix = "[RestSiteSmithPick]";

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message}\n{exception}";

        Write("ERROR", fullMessage);
    }

    private static void Write(string level, string message)
    {
        var line = $"{Prefix} [{level}] {message}";

        try
        {
            GD.Print(line);
        }
        catch
        {
            Console.WriteLine(line);
        }
    }
}
