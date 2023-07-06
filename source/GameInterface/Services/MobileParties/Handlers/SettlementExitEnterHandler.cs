using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
public class SettlementExitEnterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

    public SettlementExitEnterHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<PartySettlementEnter>(Handle);
        messageBroker.Subscribe<StartSettlementEncounter>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartySettlementEnter>(Handle);
        messageBroker.Unsubscribe<StartSettlementEncounter>(Handle);
    }

    private void Handle(MessagePayload<PartySettlementEnter> obj)
    {
        var payload = obj.What;

        if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Could not handle {messageName}, PartyId not found: {id}",
                nameof(PartySettlementEnter),
                payload.PartyId);
            return;
        }

        if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false)
        {
            Logger.Error("Could not handle {messageName}, SettlementId not found: {id}",
                nameof(PartySettlementEnter),
                payload.SettlementId);
            return;
        }

        EnterSettlementActionPatches.OverrideApplyForParty(mobileParty, settlement);
    }

    static MethodInfo InitMethod => typeof(PlayerEncounter).GetMethod(
        "Init",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new Type[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) },
        null);
    static Action<PlayerEncounter, PartyBase, PartyBase, Settlement> Init =
        InitMethod.BuildDelegate<Action<PlayerEncounter, PartyBase, PartyBase, Settlement>>();

    private static object _lock = new object();
    private void Handle(MessagePayload<StartSettlementEncounter> obj)
    {
        

        var payload = obj.What;

        if (objectManager.TryGetObject(payload.AttackerPartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Could not handle {messageName}, PartyId not found: {id}",
                nameof(StartSettlementEncounter),
                payload.AttackerPartyId);
            return;
        }

        if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false)
        {
            Logger.Error("Could not handle {messageName}, SettlementId not found: {id}",
                nameof(StartSettlementEncounter),
                payload.SettlementId);
            return;
        }

        var settlementParty = settlement.Party;
        if (settlementParty is null)
        {
            Logger.Error("Could not handle {messageName}, Settlement {settlementName} did not have a party value",
                nameof(StartSettlementEncounter),
                settlement.Name);
            return;
        }

        lock (_lock)
        {
            if (PlayerEncounter.Current is not null) return;
            PlayerEncounter.Start();

            using (EnterSettlementActionPatches.AllowedInstance)
            {
                EnterSettlementActionPatches.AllowedInstance.Instance = mobileParty;
                Init.Invoke(PlayerEncounter.Current, mobileParty.Party, settlementParty, settlement);
            }
        }
    }
}