using Common;
using Common.Util;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Utils;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class DisableTeleportationCampaignBehavior
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RegisterEvents))]
    static bool RegisterEventsPrefix() => ModInformation.IsServer;

    // Disable these methods until they are needed. Currently not patched to handle checks for Clan.PlayerClan and could cause issues for clients
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.DailyTickParty)),
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.OnHeroComesOfAge)),
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.OnPartyDisbandStarted))
    };

    static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class TeleportationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RemoveTeleportationData))]
    [HarmonyPrefix]
    public static bool RemoveTeleportationDataPrefix(TeleportationCampaignBehavior __instance, TeleportationCampaignBehavior.TeleportationData data, bool isCanceled, bool disbandTargetParty = true)
    {
        // Needs to run on main thread to avoid massive lag
        GameLoopRunner.RunOnMainThread(() =>
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
                    if (data.TargetParty.ActualClan.IsPlayerClan()) // IsPlayerClan() instead of Clan.PlayerClan check
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
}