using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// The server writes a <see cref="JournalLogEntry"/> when it ends a quest or issue as its owner, and the history is
/// saved, so a player who joins downloads every other player's finished quests. The entry carries its owner in
/// <c>_relatedObjectIds</c>, which the save keeps, so it survives a rejoin and <see cref="JournalLogEntryIsEndedPatch"/>
/// hides it from the Old Quests list of everybody else. The server still writes the history like vanilla does.
/// </summary>
[HarmonyPatch(typeof(JournalLogEntry), MethodType.Constructor, typeof(TextObject), typeof(Hero), typeof(Hero), typeof(bool), typeof(MBObjectBase[]))]
internal class JournalLogEntryOwnerPatch
{
    private const string OwnerPrefix = "coop_journal_owner:";

    // The constructor keeps the string id of every related object, so the owner rides along as one more
    [HarmonyPrefix]
    private static void Prefix(ref MBObjectBase[] relatedObjects)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ownerResolver.TryGetOwnerControllerId(relatedObjects, out var controllerId))
        {
            return;
        }

        // StringId is only settable on an allowed thread
        using (new AllowedThread())
        {
            relatedObjects = relatedObjects.Append(new MBObjectBase(OwnerPrefix + controllerId)).ToArray();
        }
    }

    internal static bool TryGetOwner(JournalLogEntry entry, out string controllerId)
    {
        controllerId = entry._relatedObjectIds
            ?.FirstOrDefault(id => id != null && id.StartsWith(OwnerPrefix, StringComparison.Ordinal))
            ?.Substring(OwnerPrefix.Length);

        return controllerId != null;
    }
}

/// <summary>
/// The Old Quests list only shows ended entries, so another player's finished quest is left out by reporting it as
/// not ended on every client except its owner's.
/// </summary>
[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal class JournalLogEntryIsEndedPatch
{
    [HarmonyPostfix]
    private static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        if (!__result || ModInformation.IsServer || !JournalLogEntryOwnerPatch.TryGetOwner(__instance, out var ownerControllerId) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
        {
            return;
        }

        __result = ownerControllerId == controllerIdProvider.ControllerId;
    }
}
