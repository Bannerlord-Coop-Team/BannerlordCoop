using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Helpers;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanPartiesVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartiesVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public ClanPartiesVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sendCoalescer = sendCoalescer;
        messageBroker.Subscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Subscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Subscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Subscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Unsubscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Unsubscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Unsubscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
    }

    private void Handle_NewClanPartyCreated(MessagePayload<NewClanPartyCreated> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.NewLeader, out var newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(data.TargetClan, out var targetClanId)) return;

        network.SendAll(new CreateNewClanParty(mainHeroId, newLeaderId, targetClanId, data.PartyGoldLowerThreshold));
    }

    private void Handle_CreateNewClanParty(MessagePayload<CreateNewClanParty> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewLeaderId, out var newLeader)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.TargetClanId, out var targetClan)) return;

            MobileParty mobileParty = MobilePartyHelper.CreateNewClanMobileParty(newLeader, targetClan);
            if (newLeader.Gold < data.PartyGoldLowerThreshold)
            {
                GiveGoldAction.ApplyBetweenCharacters(mainHero, newLeader, data.PartyGoldLowerThreshold - newLeader.Gold, false);
            }
            mobileParty.SetMoveModeHold();

            // Flush troop roster to show actual member count on clients after refresh
            objectManager.TryGetId(mobileParty.MemberRoster, out var rosterId);
            var compactId = Compact(rosterId, typeof(TroopRoster));

            sendCoalescer?.FlushInstance(compactId, network);

            network.Send(obj.Who as NetPeer, new RefreshPartiesList());
        });
    }

    private void Handle_ClanPartyLeaderChanged(MessagePayload<ClanPartyLeaderChanged> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;

        string newLeaderId = null;
        if (data.NewLeader != null && !objectManager.TryGetIdWithLogging(data.NewLeader, out newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(data.OldLeader, out var oldLeaderId)) return;

        string selectedPartyId = null;
        if (data.SelectedParty != null && !objectManager.TryGetIdWithLogging(data.SelectedParty, out selectedPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

        network.SendAll(new ChangeClanPartyLeader(mainHeroId, newLeaderId, oldLeaderId, selectedPartyId, mainPartyId));
    }

    private void Handle_ChangeClanPartyLeader(MessagePayload<ChangeClanPartyLeader> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            Hero newLeader = null;
            if (data.NewLeaderId != null && !objectManager.TryGetObjectWithLogging<Hero>(data.NewLeaderId, out newLeader)) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OldLeaderId, out var oldLeader)) return;

            MobileParty selectedParty = null;
            if (data.SelectedPartyId != null && !objectManager.TryGetObjectWithLogging<MobileParty>(data.SelectedPartyId, out selectedParty)) return;

            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            var isDisbanding = newLeader == null;
            var existingOldLeader = selectedParty?.Party?.LeaderHero != null;
            if (existingOldLeader)
            {
                if (isDisbanding) // Disbanding party
                {
                    selectedParty.RemovePartyLeader();
                    MakeHeroFugitiveAction.Apply(oldLeader, false);
                }
                else // Swapping with new leader
                {
                    TeleportHeroAction.ApplyDelayedTeleportToParty(oldLeader, mainParty);
                }
            }
            if (newLeader != null) // Teleport new leader to party
            {
                TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(newLeader, selectedParty);
            }

            // Implement ClanPartiesVM.OnDisbandCurrentyParty here to always disband the correct party.
            if (isDisbanding)
            {
                DisbandPartyAction.StartDisband(selectedParty);
            }

            // Sync GiveGoldAction.ApplyBetweenCharacters in ClanPartiesVM.OnChangeLeaderOver here instead to avoid patching the huge client side function
            // GiveGoldAction.ApplyInternal blocked on the client so OnChangeLeaderOver shouldn't manage the gold change clientside
            var partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
            if (!isDisbanding && newLeader.Gold < partyGoldLowerThreshold)
            {
                GiveGoldAction.ApplyBetweenCharacters(mainHero, newLeader, partyGoldLowerThreshold - newLeader.Gold, false);
            }

            network.Send(obj.Who as NetPeer, new RefreshPartiesList());
        });
    }
}
