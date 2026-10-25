using Common;
using Common.Util;
using HarmonyLib;
using SandBox.GauntletUI;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch]
internal class GauntletPartyScreenPatches
{
    [HarmonyPatch(typeof(GauntletPartyScreen), nameof(GauntletPartyScreen.OnResume))]
    [HarmonyPrefix]
    public static void OnResumePrefix(GauntletPartyScreen __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance._dataSource?.IsInConversation != true) return;

        RebaseOnOwnerRosters(__instance._partyState.PartyScreenLogic);
    }

    /// <summary>
    /// Brings the screen copies up to date with roster changes the server made during a conversation.
    /// </summary>
    /// <remarks>
    /// The refresher skips them while the conversation is the active state, and the DoneLogic vanilla runs
    /// on resume would Reset the stale copies back into the live rosters (e.g. a recruited lord's companions).
    /// </remarks>
    internal static void RebaseOnOwnerRosters(PartyScreenLogic logic)
    {
        using (new AllowedThread())
        {
            Rebase(logic, logic.RightOwnerParty?.MemberRoster, data => data.RightMemberRoster);
            Rebase(logic, logic.RightOwnerParty?.PrisonRoster, data => data.RightPrisonerRoster);
            Rebase(logic, logic.LeftOwnerParty?.MemberRoster, data => data.LeftMemberRoster);
            Rebase(logic, logic.LeftOwnerParty?.PrisonRoster, data => data.LeftPrisonerRoster);
        }

        logic.OnReset(false);
    }

    private static void Rebase(PartyScreenLogic logic, TroopRoster live, Func<PartyScreenData, TroopRoster> slot)
    {
        if (live == null) return;

        var visible = slot(logic.CurrentData);
        var initial = slot(logic._initialData);
        var saved = logic._savedData == null ? null : slot(logic._savedData);

        // A conversation only starts with no pending changes, so a screen bound to the live roster
        // can take it whole.
        if (ReferenceEquals(visible, live))
        {
            CopyRoster(live, initial);
            CopyRoster(live, saved);
            return;
        }

        // The screen shows its own copy (e.g. creating a clan party), so only drop what the server removed.
        RemoveMissing(live, visible);
        RemoveMissing(live, initial);
        RemoveMissing(live, saved);
    }

    private static void CopyRoster(TroopRoster live, TroopRoster copy)
    {
        if (copy == null) return;

        copy.Clear();
        copy.Add(live);
    }

    private static void RemoveMissing(TroopRoster live, TroopRoster copy)
    {
        if (copy == null) return;

        for (int i = copy.Count - 1; i >= 0; i--)
        {
            var element = copy.GetElementCopyAtIndex(i);
            if (live.GetTroopCount(element.Character) > 0) continue;

            copy.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber, 0, true);
        }

        copy.InitializeCachedData();
    }
}
