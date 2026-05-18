// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RestSiteSmithPick;

[HarmonyPatch]
internal static class RestSitePatches
{
    private static readonly PropertyInfo OwnerProperty = typeof(RestSiteOption).GetProperty(
        "Owner",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RestSiteOption.Owner property not found.");

    private static readonly PropertyInfo IsFocusedProperty = typeof(NClickableControl).GetProperty(
        "IsFocused",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("NClickableControl.IsFocused property not found.");

    private static readonly FieldInfo ExecutingOptionField = typeof(NRestSiteButton).GetField(
        "_executingOption",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("NRestSiteButton._executingOption field not found.");

    private static readonly PropertyInfo SmithCountProperty = typeof(SmithRestSiteOption).GetProperty(
        "SmithCount",
        BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("SmithRestSiteOption.SmithCount property not found.");

    private static readonly FieldInfo SelectionField = typeof(SmithRestSiteOption).GetField(
        "_selection",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SmithRestSiteOption._selection field not found.");

    private static readonly FieldInfo MinSelectBackingField = typeof(CardSelectorPrefs).GetField(
        "<MinSelect>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CardSelectorPrefs.MinSelect backing field not found.");

    private static bool _isSmithing;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static bool ModifySmithCount(SmithRestSiteOption __instance, ref Task<bool> __result, out string __state)
    {
        var owner = GetOwner(__instance);
        __state = GetProgressScope(owner);
        Config.UseProgressScope(__state);

        var upgradableCount = owner.Deck.Cards.Count(card => card.IsUpgradable);
        if (upgradableCount == 0)
        {
            Log.Info($"No upgradable cards, skipping smith. player={owner.NetId}");
            SelectionField.SetValue(__instance, Array.Empty<CardModel>());
            __result = Task.FromResult(false);
            return false;
        }

        var count = Config.PeekUpgradeCount();
        SmithCountProperty.SetValue(__instance, count);
        _isSmithing = true;
        Log.Info($"SmithCount set to {count}, delegating to original OnSelect.");

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static void AfterSmithSelect(string __state, ref Task<bool> __result)
    {
        if (__result is null)
            return;

        var original = __result;
        __result = AdvanceOnSuccessAsync(original, __state);
    }

    private static async Task<bool> AdvanceOnSuccessAsync(Task<bool> task, string progressScope)
    {
        try
        {
            var success = await task;
            if (success)
            {
                Log.Info("Smith completed, advancing cycle.");
                Config.UseProgressScope(progressScope);
                Config.AdvanceCycle();
            }
            else
            {
                Log.Info("Smith cancelled, cycle not advanced.");
            }
            return success;
        }
        finally
        {
            _isSmithing = false;
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static void ClearSmithingOnSelectException(Exception? __exception)
    {
        if (__exception is not null)
            _isSmithing = false;
    }

    /// <summary>
    /// Intercept FromDeckForUpgrade to relax MinSelect to 1,
    /// so the player can pick &lt;=N cards instead of exactly N.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade))]
    private static void RelaxMinSelect(CardSelectorPrefs prefs)
    {
        if (!_isSmithing) return;
        _isSmithing = false;

        var currentMin = (int)(MinSelectBackingField.GetValue(prefs) ?? 1);
        if (currentMin > 1)
        {
            MinSelectBackingField.SetValue(prefs, 1);
            Log.Info($"Relaxed MinSelect from {currentMin} to 1.");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton.RefreshTextState))]
    private static bool OverrideSmithDescription(NRestSiteButton __instance)
    {
        if (__instance.Option is not SmithRestSiteOption smithOption)
        {
            return true;
        }

        var room = NRestSiteRoom.Instance;
        if (room is null)
        {
            return false;
        }

        var isFocused = (bool?)IsFocusedProperty.GetValue(__instance) ?? false;
        var isExecuting = (bool?)ExecutingOptionField.GetValue(__instance) ?? false;

        if (isFocused || isExecuting)
        {
            var owner = GetOwner(smithOption);
            Config.UseProgressScope(GetProgressScope(owner));
            var count = Config.PeekUpgradeCount();
            room.SetText($"选择最多{count}张牌升级");
        }
        else
        {
            room.FadeOutOptionDescription();
        }

        return false;
    }

    private static Player GetOwner(RestSiteOption option)
    {
        return (Player?)OwnerProperty.GetValue(option)
            ?? throw new InvalidOperationException("Rest site option owner was null.");
    }

    private static string GetProgressScope(Player owner)
    {
        return $"player_{owner.NetId}";
    }
}
