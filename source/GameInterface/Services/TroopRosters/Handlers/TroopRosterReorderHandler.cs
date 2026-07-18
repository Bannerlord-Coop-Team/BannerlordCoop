using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.TroopRosters.Handlers;

/// <summary>
/// Players can set the order of their roster from the party management screen.
/// Only player rosters matter in this case so handle separately when needed.
/// This doesn't change the contents of a roster, it only re-orders it after deltas have been applied.
/// </summary>
internal class TroopRosterReorderHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterReorderHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public TroopRosterReorderHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<ApplyTroopRosterOrder>(Handle_ApplyTroopRosterOrder);
        messageBroker.Subscribe<NetworkApplyTroopRosterOrder>(Handle_NetworkApplyTroopRosterOrder);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ApplyTroopRosterOrder>(Handle_ApplyTroopRosterOrder);
        messageBroker.Unsubscribe<NetworkApplyTroopRosterOrder>(Handle_NetworkApplyTroopRosterOrder);
    }

    private void Handle_ApplyTroopRosterOrder(MessagePayload<ApplyTroopRosterOrder> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.TroopRoster, out var troopRosterId)) return;

            if (data.OrderData == null) return;

            // Apply on the server
            ApplyReorder(data.OrderData.IndexCharacterIds, data.TroopRoster);

            var message = new NetworkApplyTroopRosterOrder(troopRosterId, data.OrderData);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkApplyTroopRosterOrder(MessagePayload<NetworkApplyTroopRosterOrder> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.TroopRosterId, out var troopRoster)) return;

            var compactId = Compact(data.TroopRosterId, typeof(TroopRoster));
            sendCoalescer?.FlushInstance(compactId, network);

            // Apply on clients
            using (new AllowedThread())
            {
                ApplyReorder(data.OrderData.IndexCharacterIds, troopRoster);
            }
        });
    }

    private void ApplyReorder(Dictionary<int, string> indexCharacterIds, TroopRoster troopRoster)
    {
        foreach (var orderData in indexCharacterIds)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(orderData.Value, out var character)) return;

            var characterIndex = troopRoster.FindIndexOfTroop(character);
            troopRoster.SwapTroopsAtIndices(characterIndex, orderData.Key);
        }
    }
}
