using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class TeleportationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RegisterEvents))]
    static bool RegisterEventsPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RemoveTeleportationData))]
    [HarmonyPrefix]
    public static bool RemoveTeleportationDataPrefix(TeleportationCampaignBehavior __instance, TeleportationCampaignBehavior.TeleportationData data, bool isCanceled, bool disbandTargetParty = true)
    {
        // Needs to run on main thread to avoid massive lag
        GameThread.RunSafe(() =>
        {
            __instance._teleportationList.Remove(data);
            if (isCanceled)
            {
                if (data.TeleportingHero.IsTraveling && data.TeleportingHero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                {
                    MakeHeroFugitiveAction.Apply(data.TeleportingHero, false);
                }
                if (data.TargetParty != null)
                {
                    // IsPlayerClan() replacement for Clan.PlayerClan
                    if (data.TargetParty.ActualClan.IsPlayerClan())
                    {
                        CampaignEventDispatcher.Instance.OnPartyLeaderChangeOfferCanceled(data.TargetParty);
                    }
                    if (disbandTargetParty && data.TargetParty.IsActive && data.IsPartyLeader)
                    {
                        IDisbandPartyCampaignBehavior behavior = Campaign.Current.CampaignBehaviorManager.GetBehavior<IDisbandPartyCampaignBehavior>();
                        if (behavior != null && !behavior.IsPartyWaitingForDisband(data.TargetParty))
                        {
                            DisbandPartyAction.StartDisband(data.TargetParty);
                        }
                    }
                }
            }
        });

        return false;
    }

    [HarmonyPatch(nameof(TeleportationCampaignBehavior.DailyTickParty))]
    [HarmonyPrefix]
    public static bool DailyTickPartyPrefix(TeleportationCampaignBehavior __instance, MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            // IsPlayerClan() replacement for Clan.PlayerClan
            if (mobileParty.IsActive && mobileParty.Army == null && mobileParty.MapEvent == null && mobileParty.LeaderHero != null && mobileParty.LeaderHero.IsNoncombatant && mobileParty.LeaderHero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None && mobileParty.ActualClan != null && !mobileParty.ActualClan.IsPlayerClan() && mobileParty.ActualClan.Leader != mobileParty.LeaderHero && !mobileParty.IsInRaftState && (!mobileParty.IsCurrentlyAtSea || mobileParty.CurrentSettlement != null))
            {
                MBList<Hero> mblist = mobileParty.ActualClan.Heroes.WhereQ((Hero h) => h.IsActive && h.IsCommander && h.PartyBelongedTo == null).ToMBList<Hero>();
                if (!mblist.IsEmpty<Hero>())
                {
                    Hero leaderHero = mobileParty.LeaderHero;
                    mobileParty.RemovePartyLeader();
                    MakeHeroFugitiveAction.Apply(leaderHero, false);
                    TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(mblist.GetRandomElementInefficiently<Hero>(), mobileParty);
                }
            }
        });

        return false;
    }

    [HarmonyPatch(nameof(TeleportationCampaignBehavior.OnHeroComesOfAge))]
    [HarmonyPrefix]
    public static bool OnHeroComesOfAgePrefix(TeleportationCampaignBehavior __instance, Hero hero)
    {
        GameThread.RunSafe(() =>
        {
            // IsPlayerClan() replacement for Clan.PlayerClan 
            if (!hero.Clan.IsPlayerClan() && !hero.IsNoncombatant)
            {
                foreach (WarPartyComponent warPartyComponent in hero.Clan.WarPartyComponents)
                {
                    MobileParty mobileParty = warPartyComponent.MobileParty;
                    if (mobileParty != null && mobileParty.Army == null && mobileParty.MapEvent == null && mobileParty.LeaderHero != null && mobileParty.LeaderHero.IsNoncombatant && (!mobileParty.IsCurrentlyAtSea || mobileParty.CurrentSettlement != null))
                    {
                        Hero leaderHero = mobileParty.LeaderHero;
                        mobileParty.RemovePartyLeader();
                        MakeHeroFugitiveAction.Apply(leaderHero, false);
                        TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(hero, warPartyComponent.Party.MobileParty);
                        break;
                    }
                }
            }
        });

        return false;
    }
}