using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanManagementRefreshHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;
    private readonly Dictionary<Clan, ClanManagementRefresh> pendingRefreshes = new();

    public ClanManagementRefreshHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<ClanManagementChanged>(Handle_ClanManagementChanged);

        messageBroker.Subscribe<NetworkRefreshClanManagement>(Handle_NetworkRefreshClanManagement);
        messageBroker.Subscribe<NetworkRefreshAfterRoleAssignment>(Handle_NetworkRefreshAfterRoleAssignment);
    }

    public void Dispose()
    {
        pendingRefreshes.Clear();
        messageBroker.Unsubscribe<ClanManagementChanged>(Handle_ClanManagementChanged);

        messageBroker.Unsubscribe<NetworkRefreshClanManagement>(Handle_NetworkRefreshClanManagement);
        messageBroker.Unsubscribe<NetworkRefreshAfterRoleAssignment>(Handle_NetworkRefreshAfterRoleAssignment);
    }

    private void Handle_ClanManagementChanged(MessagePayload<ClanManagementChanged> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            var clan = obj.What.Clan;
            if (!clan.IsPlayerClan()) return;

            bool scheduleRefresh = !pendingRefreshes.TryGetValue(clan, out var sections);

            // Optimisation: Combines refresh categories for each clan to avoid duplicate messages
            pendingRefreshes[clan] = sections | obj.What.Sections;

            if (!scheduleRefresh) return;
            if (sendCoalescer == null)
            {
                SendPendingRefreshes();
                return;
            }
            if (!objectManager.TryGetIdWithLogging(clan, out var clanId))
            {
                pendingRefreshes.Remove(clan);
                return;
            }

            sendCoalescer.Enqueue(
                new CoalesceKey(nameof(NetworkRefreshClanManagement), clanId),
                new SnapshotPayload(() => CreatePendingRefreshMessage(clan, clanId)));
        });
    }

    private IMessage CreatePendingRefreshMessage(Clan clan, string clanId)
    {
        pendingRefreshes.TryGetValue(clan, out var sections);
        pendingRefreshes.Remove(clan);
        return new NetworkRefreshClanManagement(clanId, sections);
    }

    private void SendPendingRefreshes()
    {
        if (pendingRefreshes.Count == 0) return;
        var refreshes = pendingRefreshes.ToArray();
        pendingRefreshes.Clear();

        foreach (var refresh in refreshes)
        {
            if (objectManager.TryGetIdWithLogging(refresh.Key, out var clanId))
                network.SendAll(new NetworkRefreshClanManagement(clanId, refresh.Value));
        }
    }

    private void Handle_NetworkRefreshClanManagement(MessagePayload<NetworkRefreshClanManagement> obj)
    {
        Refresh(obj.What.ClanId, obj.What.Sections);
    }

    private void Refresh(string clanId, ClanManagementRefresh sections)
    {
        GameThread.RunSafe(() =>
        {
            // Check this client is looking at the updated clan
            if (!CheckClanAndTopScreen(clanId, out var clanScreen)) return;
            var vm = clanScreen._dataSource;
            if (vm == null) return;

            // Close the VM if the client is no longer a part of this clan
            if (vm._clan != Hero.MainHero.Clan)
            {
                vm.ExecuteClose();
                return;
            }

            var selectedParty = vm.ClanParties.CurrentSelectedParty?.Party;

            if ((sections & ClanManagementRefresh.Identity) != 0)
            {
                vm.Leader = new HeroVM(vm._clan.Leader);
                vm.UpdateKingdomRelatedProperties();
            }
            if ((sections & ClanManagementRefresh.Members) != 0)
            {
                var selectedHero = vm.ClanMembers.CurrentSelectedMember?.GetHero();
                vm.ClanMembers.RefreshMembersList();
                vm.ClanFiefs.RefreshAllLists();
                if (selectedHero != null) vm.ClanMembers.SelectMember(selectedHero);
            }
            if ((sections & ClanManagementRefresh.Parties) != 0)
            {
                vm.ClanParties.RefreshPartiesList();
            }
            if ((sections & (ClanManagementRefresh.Parties | ClanManagementRefresh.Identity)) != 0)
            {
                var partyItem = vm.ClanParties.Parties.Concat(vm.ClanParties.Caravans)
                    .Concat(vm.ClanParties.Garrisons).FirstOrDefault(item => item.Party == selectedParty);
                if (partyItem != null) vm.ClanParties.OnPartySelection(partyItem);
            }
            if ((sections & ClanManagementRefresh.Income) != 0)
            {
                var selectedWorkshop = vm.ClanIncome.CurrentSelectedIncome?.Workshop;
                var selectedAlley = vm.ClanIncome.CurrentSelectedAlley?.Alley;
                vm.ClanIncome.RefreshList();
                if (selectedWorkshop != null) vm.ClanIncome.SelectWorkshop(selectedWorkshop);
                if (selectedAlley != null) vm.ClanIncome.SelectAlley(selectedAlley);
            }

            vm.RefreshDailyValues();
        }, context: "ClanRefresh.Management");
    }

    private void Handle_NetworkRefreshAfterRoleAssignment(MessagePayload<NetworkRefreshAfterRoleAssignment> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (ScreenManager.TopScreen is not GauntletClanScreen clanScreen ||
                clanScreen._dataSource == null) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            foreach (var partyItemVM in clanScreen._dataSource.ClanParties._parties)
            {
                if (partyItemVM.Party.IsMobile && partyItemVM.Party.MobileParty == mobileParty)
                {
                    partyItemVM.OnRoleAssigned();
                    break;
                }
            }
        }, context: "ClanRefresh.RoleAssignment");
    }

    private bool CheckClanAndTopScreen(string clanId, out GauntletClanScreen clanScreen)
    {
        clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
        if (clanScreen == null) return false;

        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return false;
        if (clan != clanScreen._dataSource?._clan && clan != Clan.PlayerClan) return false;

        return true;
    }
}
