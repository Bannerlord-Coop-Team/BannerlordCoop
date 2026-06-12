using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers;

internal class SettlementMenuOverlayVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementMenuOverlayVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public SettlementMenuOverlayVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ClanMembersAssignedToSettlement>(Handle_ClanMembersAssignedToSettlement);
        messageBroker.Subscribe<AssignClanMembersToSettlement>(Handle_AssignClanMembersToSettlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanMembersAssignedToSettlement>(Handle_ClanMembersAssignedToSettlement);
        messageBroker.Unsubscribe<AssignClanMembersToSettlement>(Handle_AssignClanMembersToSettlement);
    }

    private void Handle_ClanMembersAssignedToSettlement(MessagePayload<ClanMembersAssignedToSettlement> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        List<string> heroIds = new();
        foreach (var hero in obj.What.LeftHeroes)
        {
            if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) continue;

            heroIds.Add(heroId);
        }

        var message = new AssignClanMembersToSettlement(settlementId, mainPartyId, heroIds);
        network.SendAll(message);
    }

    private void Handle_AssignClanMembersToSettlement(MessagePayload<AssignClanMembersToSettlement> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

        foreach (var heroId in obj.What.HeroIds)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) continue;

            mainParty.MemberRoster.RemoveTroop(hero.CharacterObject, 1, default, 0);
            if (!settlement.HeroesWithoutParty.Contains(hero))
            {
                EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
            }
        }
    }
}
