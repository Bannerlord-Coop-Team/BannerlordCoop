using Common;
using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Server-side hooks that keep settlement location rosters live. Vanilla
/// <see cref="HeroAgentSpawnCampaignBehavior"/> refreshes hero placement on governor and prisoner
/// events, but its handlers are gated on the local player being inside the settlement, which is never
/// true on the stationary host, so they no-op there. These postfixes run after the inert vanilla
/// handlers and forward the affected heroes to the population tracker, which refreshes any settlement
/// that currently has player visitors and re-broadcasts its roster.
/// </summary>
[HarmonyPatch(typeof(HeroAgentSpawnCampaignBehavior))]
internal class HeroAgentSpawnRosterPatches
{
    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.OnGovernorChanged))]
    [HarmonyPostfix]
    static void OnGovernorChangedPostfix(Town town, Hero oldGovernor, Hero newGovernor)
    {
        if (!ModInformation.IsServer) return;
        if (town?.Settlement == null) return;

        // The old governor moves out of the governor spot; the new one moves into it. Mirror vanilla
        // and refresh both - the handler ignores the nulls.
        MessageBroker.Instance.Publish(null, new SettlementRosterHeroesChanged(town.Settlement, new[] { oldGovernor, newGovernor }));
    }

    [HarmonyPatch(nameof(HeroAgentSpawnCampaignBehavior.OnPrisonersChangeInSettlement))]
    [HarmonyPostfix]
    static void OnPrisonersChangeInSettlementPostfix(
        Settlement settlement, FlattenedTroopRoster prisonerRoster, Hero prisonerHero)
    {
        if (!ModInformation.IsServer) return;
        // Vanilla only refreshes prisoner placement for fortifications (towns/castles).
        if (settlement == null || !settlement.IsFortification) return;

        var heroes = new List<Hero>();

        // Mirror vanilla: skip the active conversation partner so an ongoing dialog is left alone.
        if (prisonerHero != null && prisonerHero != Hero.OneToOneConversationHero)
        {
            heroes.Add(prisonerHero);
        }

        if (prisonerRoster != null)
        {
            foreach (var element in prisonerRoster)
            {
                if (element.Troop?.IsHero == true && element.Troop.HeroObject != Hero.OneToOneConversationHero)
                {
                    heroes.Add(element.Troop.HeroObject);
                }
            }
        }

        if (heroes.Count == 0) return;

        MessageBroker.Instance.Publish(null, new SettlementRosterHeroesChanged(settlement, heroes.ToArray()));
    }
}
