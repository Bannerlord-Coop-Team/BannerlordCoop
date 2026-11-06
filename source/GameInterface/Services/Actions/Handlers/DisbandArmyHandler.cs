using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

/// <summary>
/// Handler for <see cref="DisbandArmyAction"/> messages.
/// </summary>
namespace GameInterface.Services.Actions.Handlers;

public class DisbandArmyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisbandArmyHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public DisbandArmyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<DisbandArmyApplyInternal>(HandleDisbandArmyApplyInternal);
        messageBroker.Subscribe<NetworkDisbandArmyApplyInternal>(HandleNetworkDisbandArmyApplyInternal);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DisbandArmyApplyInternal>(HandleDisbandArmyApplyInternal);
        messageBroker.Unsubscribe<NetworkDisbandArmyApplyInternal>(HandleNetworkDisbandArmyApplyInternal);
    }

    private void HandleDisbandArmyApplyInternal(MessagePayload<DisbandArmyApplyInternal> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.ClientParty, out var clientPartyId)) return;

        var message = new NetworkDisbandArmyApplyInternal(armyId, clientPartyId);

        network.SendAll(message);
    }

    private void HandleNetworkDisbandArmyApplyInternal(MessagePayload<NetworkDisbandArmyApplyInternal> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetObjectWithLogging(obj.ArmyId, out Army army) == false) return;
            if (objectManager.TryGetObjectWithLogging(obj.ClientPartyId, out MobileParty clientParty) == false) return;
            try
            {
                ResolvedMainHeroContext.ResolvedMainHero = clientParty.LeaderHero;
                DisbandArmyActionPatches.DisbandArmyApplyInternal(army, clientParty);
            }
            finally
            {
                ResolvedMainHeroContext.ResolvedMainHero = null;
            }
        });
    }
}
