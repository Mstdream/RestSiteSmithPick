// Original work Copyright (c) 2025 chenyu. Licensed under Apache License 2.0.
// Modified by Mst, 2026.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static bool ModifySmithCount(SmithRestSiteOption __instance, ref Task<bool> __result)
    {
        var owner = GetOwner(__instance);
        var upgradableCount = owner.Deck.Cards.Count(card => card.IsUpgradable);
        if (upgradableCount == 0)
        {
            Log.Info($"No upgradable cards, skipping smith. player={owner.NetId}");
            SelectionField.SetValue(__instance, Array.Empty<CardModel>());
            __result = Task.FromResult(false);
            return false;
        }

        var count = Config.NextUpgradeCount();
        SmithCountProperty.SetValue(__instance, count);
        Log.Info($"SmithCount set to {count}, delegating to original OnSelect.");

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton.RefreshTextState))]
    private static bool OverrideSmithDescription(NRestSiteButton __instance)
    {
        if (__instance.Option is not SmithRestSiteOption)
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
            var count = Config.PeekUpgradeCount();
            room.SetText($"选择{count}张牌升级");
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
}
