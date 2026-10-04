#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>Stages real inventories and observes the production player barter path during live tests.</summary>
internal sealed class LargeInventoryBarterFixtureCommand : ICoopCommand, IDisposable
{
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly Dictionary<string, ItemRosterElement[]> originalInventories = new Dictionary<string, ItemRosterElement[]>();
    private bool watching;
    private int publishedOffers;
    private int receivedOffers;

    public LargeInventoryBarterFixtureCommand(IObjectManager objectManager, IMessageBroker messageBroker, INetwork network)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.network = network;
    }

    public string Prefix => "coop.debug.barter";
    public string Name => "large_inventory";
    public string Description => "Stages, restores and inspects real player barter, or requests its production actions.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("operation", "stage, restore, watch, inspect, request, option or offer", true),
        new ExpectedArgs("argument", "Registered mobile party id, or trade option", false),
        new ExpectedArgs("count", "Distinct item count for stage, from 1 to 512", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count < 1) return Failed("An operation is required.");
        if (args[0] == "watch" && args.Count == 1)
        {
            Dispose();
            Interlocked.Exchange(ref publishedOffers, 0);
            Interlocked.Exchange(ref receivedOffers, 0);
            messageBroker.Subscribe<PlayerPartyTradeOfferChanged>(ObservePublished);
            messageBroker.Subscribe<NetworkPlayerPartyTradeOfferUpdated>(ObserveReceived);
            watching = true;
            return Succeeded("Watching real player barter offers.");
        }
        if (args[0] == "stage" || args[0] == "restore") return ChangeInventory(args);
        if (args[0] == "inspect" && args.Count == 1) return Inspect();
        if (ModInformation.IsServer) return Failed("Trade actions must originate from a client.");
        if (args[0] == "request" && args.Count == 2)
        {
            if (!objectManager.TryGetObject<MobileParty>(args[1], out var other) ||
                MobileParty.MainParty == null || !objectManager.TryGetId(other.Party, out var otherId) ||
                !objectManager.TryGetId(MobileParty.MainParty.Party, out var localId))
                return Failed("Both player parties must be registered.");
            network.SendAll(new NetworkRequestConversation(otherId, localId, false,
                ConversationRestartSource.PlayerEncounter, false, Guid.NewGuid().ToString("N")));
            return Succeeded("Requested the production player conversation.");
        }
        if (args[0] == "option" && args.Count == 2 &&
            Enum.TryParse(args[1], out PlayerPartyInteractionOption option) &&
            (option == PlayerPartyInteractionOption.TradeProposal || option == PlayerPartyInteractionOption.AcceptProposal ||
             option == PlayerPartyInteractionOption.Leave))
        {
            if (!PlayerPartyInteractionDialogState.IsOptionEnabled(option)) return Failed("Option is not enabled.");
            PlayerPartyInteractionDialogState.Submit(option);
            return Succeeded("Submitted the production trade option.");
        }
        if (args[0] == "offer" && args.Count == 1)
        {
            var vm = PlayerPartyTradeContext.ActiveBarterVM;
            if (vm?.InitializationIsOver != true) return Failed("No initialized player barter screen.");
            var item = vm.RightItemList.FirstOrDefault(x => PlayerPartyTradeContext.CanOffer(x.Barterable));
            if (item == null) return Failed("No locally owned item to offer.");
            vm.OfferItemAdd(item, 1);
            return Inspect();
        }
        return Failed("Invalid operation or arguments.");
    }

    private CoopCommandResult ChangeInventory(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient) return Failed("Inventory staging and restoration require the server.");
        if ((args[0] == "stage" && args.Count != 3) || (args[0] == "restore" && args.Count != 2))
            return Failed("stage requires party id and item count; restore requires party id.");
        if (!objectManager.TryGetObject<MobileParty>(args[1], out var party)) return Failed("Party is not registered.");
        if (args[0] == "restore")
        {
            if (!originalInventories.TryGetValue(args[1], out var original)) return Failed("No staged inventory for this party.");
            party.ItemRoster.Clear();
            foreach (var item in original) party.ItemRoster.AddToCounts(item.EquipmentElement, item.Amount);
            originalInventories.Remove(args[1]);
            return Succeeded($"Restored {args[1]} inventory with {party.ItemRoster.Count} entries.");
        }
        if (!int.TryParse(args[2], out var count) || count < 1 || count > 512)
            return Failed("Item count must be from 1 to 512.");
        if (originalInventories.ContainsKey(args[1])) return Failed("Restore the previous staged inventory first.");
        var items = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
            .Where(x => x != null && objectManager.TryGetId(x, out _))
            .OrderBy(x => x.Value).ThenBy(x => x.StringId, StringComparer.Ordinal).Take(count).ToArray();
        if (items.Length != count) return Failed("Not enough registered item types.");
        originalInventories.Add(args[1], party.ItemRoster.ToArray());
        foreach (var item in items) party.ItemRoster.AddToCounts(new EquipmentElement(item), 5);
        return Succeeded(JsonConvert.SerializeObject(new { partyId = args[1], addedDistinctItems = count,
            inventoryEntries = party.ItemRoster.Count, firstItem = items[0].StringId, lastItem = items[count - 1].StringId }));
    }

    private CoopCommandResult Inspect()
    {
        var vm = PlayerPartyTradeContext.ActiveBarterVM;
        return Succeeded(JsonConvert.SerializeObject(new
        {
            server = ModInformation.IsServer,
            sessionId = PlayerPartyTradeContext.SessionId,
            phase = PlayerPartyInteractionDialogState.Phase.ToString(),
            initialized = vm?.InitializationIsOver == true,
            leftItems = vm?.LeftItemList.Count ?? 0,
            rightItems = vm?.RightItemList.Count ?? 0,
            leftOfferItems = vm?.LeftOfferList.Count ?? 0,
            rightOfferItems = vm?.RightOfferList.Count ?? 0,
            publishedOffers = Volatile.Read(ref publishedOffers),
            receivedOffers = Volatile.Read(ref receivedOffers),
            observing = watching,
        }));
    }

    private void ObservePublished(MessagePayload<PlayerPartyTradeOfferChanged> payload) => Interlocked.Increment(ref publishedOffers);
    private void ObserveReceived(MessagePayload<NetworkPlayerPartyTradeOfferUpdated> payload) => Interlocked.Increment(ref receivedOffers);
    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    public void Dispose()
    {
        if (!watching) return;
        messageBroker.Unsubscribe<PlayerPartyTradeOfferChanged>(ObservePublished);
        messageBroker.Unsubscribe<NetworkPlayerPartyTradeOfferUpdated>(ObserveReceived);
        watching = false;
    }
}
#endif
