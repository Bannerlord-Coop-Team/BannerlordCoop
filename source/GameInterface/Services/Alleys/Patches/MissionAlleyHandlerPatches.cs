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
/// result and sends it as a request; the vanilla method's own local effects (returning the chosen
/// troops out of the player's party) replicate through the existing party-screen sync.
/// </summary>
[HarmonyPatch(typeof(MissionAlleyHandler))]
internal class MissionAlleyHandlerPatches
{
    [HarmonyPatch("OnPartyScreenDoneClicked")]
    [HarmonyPostfix]
    private static void OnPartyScreenDoneClickedPostfix(TroopRoster leftMemberRoster)
    {
        if (ModInformation.IsServer) return;

        var alley = CampaignMission.Current?.LastVisitedAlley;
        if (alley == null || leftMemberRoster == null) return;

        // The acquiring player owns the alley (like vanilla's SetOwner(Hero.MainHero)); the chosen
        // clan member in the roster is the separate overseer (AssignedClanMember).
        Hero owner = Hero.MainHero;
        if (owner == null) return;

        Hero overseer = leftMemberRoster.GetTroopRoster()
            .FirstOrDefault(e => e.Character != null && e.Character.IsHero).Character?.HeroObject;
        if (overseer == null) return;

        MessageBroker.Instance.Publish(alley, new AlleyAcquiredRequested(alley, owner, overseer, leftMemberRoster));
    }
}
