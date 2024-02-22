using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles control of mobile party entities.
/// </summary>
internal class MobilePartyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyInterface partyInterface;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IObjectManager objectManager;
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyHandler>();

    public MobilePartyHandler(
        IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ChangeWagePaymentLimit>(HandleWagePaymentLimit); // server
        messageBroker.Subscribe<WagePaymentApprovedOthers>(HandleWagePaymentLimitOtherClients); // all other clients
    }

    private void HandleWagePaymentLimitOtherClients(MessagePayload<WagePaymentApprovedOthers> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<MobileParty>(obj.MobilePartyId, out var mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({MobilePartyId})", obj.MobilePartyId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                mobileParty.SetWagePaymentLimit(obj.WageAmount);
            }
        });
    }

    private void HandleWagePaymentLimit(MessagePayload<ChangeWagePaymentLimit> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<MobileParty>(obj.MobilePartyId, out var mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({MobilePartyId})", obj.MobilePartyId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                mobileParty.SetWagePaymentLimit(obj.WageAmount);
            }
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeWagePaymentLimit>(HandleWagePaymentLimit);
        messageBroker.Unsubscribe<WagePaymentApprovedOthers>(HandleWagePaymentLimitOtherClients);

    }
}
