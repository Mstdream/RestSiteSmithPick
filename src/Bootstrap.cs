// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System.Reflection;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace RestSiteSmithPick;

[ModInitializer(nameof(Initialize))]
internal static class Bootstrap
{
    private static int _initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        Log.Info("Initializing.");
        Config.Load();
        var harmony = new Harmony("rest_site_smith_pick");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log.Info("Applied Harmony patches.");
    }
}
