using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Helpers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanPartiesVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartiesVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanPartiesVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Subscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Subscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Subscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
        messageBroker.Subscribe<ClanPartyDisbanded>(Handle_ClanPartyDisbanded);
        messageBroker.Subscribe<DisbandClanParty>(Handle_DisbandClanParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewClanPartyCreated>(Handle_NewClanPartyCreated);
        messageBroker.Unsubscribe<CreateNewClanParty>(Handle_CreateNewClanParty);
        messageBroker.Unsubscribe<ClanPartyLeaderChanged>(Handle_ClanPartyLeaderChanged);
        messageBroker.Unsubscribe<ChangeClanPartyLeader>(Handle_ChangeClanPartyLeader);
        messageBroker.Unsubscribe<ClanPartyDisbanded>(Handle_ClanPartyDisbanded);
        messageBroker.Unsubscribe<DisbandClanParty>(Handle_DisbandClanParty);
    }

    private void Handle_NewClanPartyCreated(MessagePayload<NewClanPartyCreated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewLeader, out var newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.TargetClan, out var targetClanId)) return;

        network.SendAll(new CreateNewClanParty(mainHeroId, newLeaderId, targetClanId, obj.What.PartyGoldLowerThreshold));
    }

    private void Handle_CreateNewClanParty(MessagePayload<CreateNewClanParty> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewLeaderId, out var newLeader)) return;
        if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.TargetClanId, out var targetClan)) return;

        MobileParty mobileParty = MobilePartyHelper.CreateNewClanMobileParty(newLeader, targetClan);
        if (newLeader.Gold < obj.What.PartyGoldLowerThreshold)
        {
            GiveGoldAction.ApplyBetweenCharacters(mainHero, newLeader, obj.What.PartyGoldLowerThreshold - newLeader.Gold, false);
        }
        mobileParty.SetMoveModeHold();
    }

    private void Handle_ClanPartyLeaderChanged(MessagePayload<ClanPartyLeaderChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewLeader, out var newLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OldLeader, out var oldLeaderId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.SelectedParty, out var selectedPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        network.SendAll(new ChangeClanPartyLeader(mainHeroId, newLeaderId, oldLeaderId, selectedPartyId, mainPartyId));
    }

    private void Handle_ChangeClanPartyLeader(MessagePayload<ChangeClanPartyLeader> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;

        Hero newLeader = null;
        if (obj.What.NewLeaderId != null && !objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewLeaderId, out newLeader)) return;

        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OldLeaderId, out var oldLeader)) return;

        MobileParty selectedParty = null;
        if (obj.What.SelectedPartyId != null && !objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.SelectedPartyId, out selectedParty)) return;
        
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

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

        // Sync GiveGoldAction.ApplyBetweenCharacters in ClanPartiesVM.OnChangeLeaderOver here instead to avoid patching the huge client side function
        // GiveGoldAction.ApplyInternal now blocked on the client so OnChangeLeaderOver shouldn't manage the gold change clientside
        var partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
        if (!isDisbanding && newLeader.Gold < partyGoldLowerThreshold)
        {
            GiveGoldAction.ApplyBetweenCharacters(mainHero, newLeader, partyGoldLowerThreshold - newLeader.Gold, false);
        }
    }

    private void Handle_ClanPartyDisbanded(MessagePayload<ClanPartyDisbanded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.SelectedParty, out var selectedPartyId)) return;

        network.SendAll(new DisbandClanParty(selectedPartyId));
    }

    private void Handle_DisbandClanParty(MessagePayload<DisbandClanParty> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.SelectedPartyId, out var selectedParty)) return;

        DisbandPartyAction.StartDisband(selectedParty);
    }
}
