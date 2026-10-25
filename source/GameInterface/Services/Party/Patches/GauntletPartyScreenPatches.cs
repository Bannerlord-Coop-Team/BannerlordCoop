﻿using Common;
using Common.Util;
using HarmonyLib;
using SandBox.GauntletUI;
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
    /// Rebuilds the screen data from the parties' live rosters after a conversation.
    /// </summary>
    /// <remarks>
    /// The server can change any roster during the talk, e.g. freeing the lord's clan members when he
    /// joins, and the screen copies miss those changes because the party screen isn't the active state.
    /// Vanilla then runs DoneLogic, whose Reset would put the stale troops back into the live rosters.
    /// A conversation only starts with no pending changes, so the live rosters are the right baseline.
    /// </remarks>
    internal static void RebaseOnOwnerRosters(PartyScreenLogic logic)
    {
        using (new AllowedThread())
        {
            CopyLiveRoster(logic.RightOwnerParty?.MemberRoster, logic.CurrentData.RightMemberRoster);
            CopyLiveRoster(logic.RightOwnerParty?.PrisonRoster, logic.CurrentData.RightPrisonerRoster);
            CopyLiveRoster(logic.LeftOwnerParty?.MemberRoster, logic.CurrentData.LeftMemberRoster);
            CopyLiveRoster(logic.LeftOwnerParty?.PrisonRoster, logic.CurrentData.LeftPrisonerRoster);

            logic._initialData.CopyFromScreenData(logic.CurrentData);
            if (logic._savedData != null) logic.SavePartyScreenData();
        }

        logic.OnReset(false);
    }

    private static void CopyLiveRoster(TroopRoster live, TroopRoster visible)
    {
        if (live == null || visible == null || ReferenceEquals(live, visible)) return;

        visible.Clear();
        visible.Add(live);
    }
}
