using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="CaravanPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class CaravanPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent));

    private static bool Prefix(CaravanPartyComponent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(CaravanPartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch(typeof(CaravanPartyComponent))]
internal class TEST
{
    [HarmonyPatch(nameof(CaravanPartyComponent.CreateCaravanParty))]
    [HarmonyPrefix]
    public static bool ItemRosterSetterPostfix(Hero caravanOwner, Settlement spawnSettlement, bool isInitialSpawn, Hero caravanLeader, ItemRoster caravanItems , int troopToBeGiven, bool isElite, ref MobileParty __result)
    {
        MobileParty mobileParty2 = MobileParty.CreateParty("caravan_template_" + spawnSettlement.Culture.StringId.ToLower() + "_1", new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader), delegate (MobileParty mobileParty)
        {
            (mobileParty.PartyComponent as CaravanPartyComponent).InitializeCaravanOnCreation(mobileParty, caravanLeader, caravanItems, troopToBeGiven, isElite);
        });
        if (spawnSettlement.Party.MapEvent == null && spawnSettlement.SiegeEvent == null)
        {
            mobileParty2.Ai.SetMoveGoToSettlement(spawnSettlement);
            mobileParty2.Ai.RecalculateShortTermAi();
            EnterSettlementAction.ApplyForParty(mobileParty2, spawnSettlement);
        }
        else
        {
            mobileParty2.Ai.SetMoveModeHold();
        }
        if (mobileParty2.LeaderHero != null)
        {
            CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty2.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);
        }
        __result = mobileParty2;
        return false;
    }
}