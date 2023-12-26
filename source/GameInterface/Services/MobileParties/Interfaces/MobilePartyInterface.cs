using Common;
using Common.Extensions;
using Common.Logging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Abstracts interacting with the MobileParty class in game
/// </summary>
internal interface IMobilePartyInterface : IGameAbstraction
{
    /// <summary>
    /// Starts a settlement encounter for the player, bypasses patch skip rules
    /// </summary>
    /// <param name="partyId">Party to enter settlement as StringId</param>
    /// <param name="settlementId">Settlement to enter as StringId</param>
    void StartPlayerSettlementEncounter(string partyId, string settlementId);
    /// <summary>
    /// Forces the player party to leave their current settlement encounter
    /// </summary>
    void EndPlayerSettlementEncounter();
    /// <summary>
    /// Forces party to enter settlement, bypasses patch skip rules
    /// </summary>
    /// <param name="partyId">Party to leave current settlement</param>
    void LeaveSettlement(string partyId);
    /// <summary>
    /// Handles the initialization of a newly transfered party
    /// </summary>
    /// <param name="party"></param>
    void ManageNewParty(MobileParty party);
    /// <summary>
    /// Registers all parties in the game as controlled by <paramref name="ownerId"/>.
    /// </summary>
    /// <param name="ownerId">Owner to assign all parties</param>
    void RegisterAllPartiesAsControlled(string ownerId);
    /// <summary>
    /// Forces party to enter settlement, bypasses patch skip rules
    /// </summary>
    /// <param name="partyId">Party to enter settlement as StringId</param>
    /// <param name="settlementId">Settlement to enter as StringId</param>
    void EnterSettlement(string partyId, string settlementId);
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyInterface>();
    private static readonly MethodInfo PartyBase_OnFinishLoadState = typeof(PartyBase).GetMethod("OnFinishLoadState", BindingFlags.NonPublic | BindingFlags.Instance);

    private readonly IMobilePartyRegistry partyRegistry;
    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public MobilePartyInterface(
        IMobilePartyRegistry partyRegistry,
        IObjectManager objectManager,
        IControlledEntityRegistry controlledEntityRegistry)
    {
        this.partyRegistry = partyRegistry;
        this.objectManager = objectManager;
        this.controlledEntityRegistry = controlledEntityRegistry;
    }

    public void ManageNewParty(MobileParty party)
    {
        party.IsVisible = true;

        PartyBase_OnFinishLoadState.Invoke(party.Party, null);
    }

    public void RegisterAllPartiesAsControlled(string ownerId)
    {
        foreach(var party in partyRegistry)
        {
            controlledEntityRegistry.RegisterAsControlled(ownerId, party.Key);
        }
    }

    private static  AllowedInstance<MobileParty> allowedInstance = new AllowedInstance<MobileParty>();
    private static MethodInfo InitMethod => typeof(PlayerEncounter).GetMethod(
        "Init",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new Type[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) },
        null);
    private static Action<PlayerEncounter, PartyBase, PartyBase, Settlement> Init =
        InitMethod.BuildDelegate<Action<PlayerEncounter, PartyBase, PartyBase, Settlement>>();
    public void StartPlayerSettlementEncounter(string partyId, string settlementId)
    {
        if (objectManager.TryGetObject(partyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", partyId);
            return;
        }

        if (objectManager.TryGetObject(settlementId, out Settlement settlement) == false)
        {
            Logger.Error("SettlementId not found: {id}", settlementId);
            return;
        }

        var settlementParty = settlement.Party;
        if (settlementParty is null)
        {
            Logger.Error("Settlement {settlementName} did not have a party value", settlement.Name);
            return;
        }

        
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (EnterSettlementActionPatches.AllowedInstance)
            {
                EnterSettlementActionPatches.AllowedInstance.Instance = mobileParty;
                if (PlayerEncounter.Current is not null) return;
                PlayerEncounter.Start();
                Init(PlayerEncounter.Current, mobileParty.Party, settlementParty, settlement);
            }
        });
    }

    public void EndPlayerSettlementEncounter()
    {
        GameLoopRunner.RunOnMainThread(PlayerLeaveSettlementPatch.OverrideLeaveConsequence);
    }

    public void EnterSettlement(string partyId, string settlementId)
    {
        if (objectManager.TryGetObject(partyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", partyId);
            return;
        }

        if (objectManager.TryGetObject(settlementId, out Settlement settlement) == false)
        {
            Logger.Error("SettlementId not found: {id}", settlementId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            EnterSettlementActionPatches.OverrideApplyForParty(mobileParty, settlement);
        });
    }

    public void LeaveSettlement(string partyId)
    {
        if (objectManager.TryGetObject(partyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", partyId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            LeaveSettlementActionPatches.OverrideApplyForParty(mobileParty);
        });
    }
}
