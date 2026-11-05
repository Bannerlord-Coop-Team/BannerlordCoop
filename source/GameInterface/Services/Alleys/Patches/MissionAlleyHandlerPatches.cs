using Common;
using Common.Messaging;
using GameInterface.Services.Alleys.Messages;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Routes alley acquisition (winning the alley fight and taking the alley over) to the server.
/// The fight and its take-over UI are a local solo mission on the acquiring client, and the
/// authoritative result is only fully known at the take-over party screen's done button. Since
/// <c>Alley.SetOwner</c> is server-authoritative (the host has no main hero), this captures that
/// result and sends it as a request instead of running the vanilla done button. The server checks it
/// and only then takes the troops out of the party and sends the overseer to the alley, so a refused
/// take over changes nothing on any peer.
/// </summary>
[HarmonyPatch(typeof(MissionAlleyHandler))]
internal class MissionAlleyHandlerPatches
{
    // Who held the alley when the fight was won. Another player's take over can reach this client while
    // it sits in the troop screen, and the server must judge the request by the owner this player beat.
    private static Hero ownerWhenFightWon;

    [HarmonyPatch("OnAlleyFightEnd")]
    [HarmonyPrefix]
    private static void OnAlleyFightEndPrefix(bool isPlayerSideWon)
    {
        if (ModInformation.IsServer || !isPlayerSideWon) return;

        ownerWhenFightWon = CampaignMission.Current?.LastVisitedAlley?.Owner;
    }

    [HarmonyPatch("OnPartyScreenDoneClicked")]
    [HarmonyPrefix]
    private static bool OnPartyScreenDoneClickedPrefix(TroopRoster leftMemberRoster, TroopRoster rightMemberRoster, ref bool __result)
    {
        if (ModInformation.IsServer) return true;

        // Vanilla would send the overseer away and take the troops out of the party here. The server does
        // both once it accepts, so this only sends the request and lets the screen close.
        __result = true;

        var alley = CampaignMission.Current?.LastVisitedAlley;
        if (alley == null || leftMemberRoster == null) return false;

        // The right roster is this screen's copy of the party. Putting the chosen troops back on it keeps the
        // screen's own Done from sending the troop move, which the server makes itself on an accepted take over.
        foreach (var element in leftMemberRoster.GetTroopRoster())
        {
            if (element.Character != null && !element.Character.IsHero)
                rightMemberRoster?.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp);
        }

        // The acquiring player owns the alley (like vanilla's SetOwner(Hero.MainHero)); the chosen
        // clan member in the roster is the separate overseer (AssignedClanMember).
        Hero owner = Hero.MainHero;
        if (owner == null) return false;

        Hero overseer = leftMemberRoster.GetTroopRoster()
            .FirstOrDefault(e => e.Character != null && e.Character.IsHero).Character?.HeroObject;
        if (overseer == null) return false;

        MessageBroker.Instance.Publish(alley, new AlleyAcquiredRequested(alley, owner, overseer, leftMemberRoster, ownerWhenFightWon));
        return false;
    }
}
