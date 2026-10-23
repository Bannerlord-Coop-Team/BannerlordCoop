using Common;
using GameInterface.Services.Characters.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterRelationCampaignBehavior))]
internal class DisableCharacterRelationCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(CharacterRelationCampaignBehavior))]
internal class CharacterRelationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    public static bool OnHeroKilledPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (!ContainerProvider.TryResolve<ICharacterRelationCampaignBehaviorInterface>(out var characterRelationBehaviorInterface)) return true;

        characterRelationBehaviorInterface.OnHeroKilled(victim, killer, detail, showNotification);

        return false;
    }

    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.OnPrisonerDonatedToSettlement))]
    [HarmonyPrefix]
    public static bool OnPrisonerDonatedToSettlementPrefix(MobileParty donatingParty, FlattenedTroopRoster donatedPrisoners, Settlement donatedSettlement)
    {
        if (!ContainerProvider.TryResolve<ICharacterRelationCampaignBehaviorInterface>(out var characterRelationBehaviorInterface)) return true;

        characterRelationBehaviorInterface.OnPrisonerDonatedToSettlement(donatingParty, donatedPrisoners, donatedSettlement);

        return false;
    }

    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix()
    {
        if (!ContainerProvider.TryResolve<ICharacterRelationCampaignBehaviorInterface>(out var characterRelationBehaviorInterface)) return true;

        characterRelationBehaviorInterface.DailyTick();

        return false;
    }

    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnSettlementOwnerChangedPrefix(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (!ContainerProvider.TryResolve<ICharacterRelationCampaignBehaviorInterface>(out var characterRelationBehaviorInterface)) return true;

        characterRelationBehaviorInterface.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail);

        return false;
    }

    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.OnRaidCompleted))]
    [HarmonyPrefix]
    public static bool OnRaidCompletedPrefix(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (!ContainerProvider.TryResolve<ICharacterRelationCampaignBehaviorInterface>(out var characterRelationBehaviorInterface)) return true;

        characterRelationBehaviorInterface.OnRaidCompleted(winnerSide, raidEvent);

        return false;
    }
}