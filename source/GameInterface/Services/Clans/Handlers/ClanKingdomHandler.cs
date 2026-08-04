using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanKingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(KingdomInterface));
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanKingdomHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    { 
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<SetClanKingdom>(HandleSetClanKingdom);
        messageBroker.Subscribe<NetworkSetClanKingdom>(HandleNetworkSetClanKingdom);
        messageBroker.Subscribe<OnClanChangedKingdom>(HandleOnClanChangedKingdom);
        messageBroker.Subscribe<NetworkOnClanChangedKingdom>(HandleNetworkOnClanChangedKingdom);
        messageBroker.Subscribe<OnClanSupported>(HandleOnClanSupported);
        messageBroker.Subscribe<NetworkOnClanSupported>(HandleNetworkOnClanSupported);
        messageBroker.Subscribe<NetworkOnClanSupportedApplied>(HandleNetworkOnClanSupportedApplied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetClanKingdom>(HandleSetClanKingdom);
        messageBroker.Unsubscribe<NetworkSetClanKingdom>(HandleNetworkSetClanKingdom);
        messageBroker.Unsubscribe<OnClanChangedKingdom>(HandleOnClanChangedKingdom);
        messageBroker.Unsubscribe<NetworkOnClanChangedKingdom>(HandleNetworkOnClanChangedKingdom);
        messageBroker.Unsubscribe<OnClanSupported>(HandleOnClanSupported);
        messageBroker.Unsubscribe<NetworkOnClanSupported>(HandleNetworkOnClanSupported);
        messageBroker.Unsubscribe<NetworkOnClanSupportedApplied>(HandleNetworkOnClanSupportedApplied);
    }

    private void HandleSetClanKingdom(MessagePayload<SetClanKingdom> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;
        string kingdomId = null;
        if (payload.What.Kingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.Kingdom, out kingdomId)) return;
        }

        network.SendAll(new NetworkSetClanKingdom(clanId, kingdomId));
    }

    private void HandleNetworkSetClanKingdom(MessagePayload<NetworkSetClanKingdom> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.ClanId, out var clan)) return;
            Kingdom kingdom = null;
            if (payload.What.KingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out kingdom)) return;
            }

            using (new AllowedThread())
            {
                clan.SetKingdomInternal(kingdom);
            }
        });
    }

    private void HandleOnClanChangedKingdom(MessagePayload<OnClanChangedKingdom> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Clan, out var clanId)) return;
        string oldKingdomId = null;
        if (payload.What.OldKingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.OldKingdom, out oldKingdomId)) return;
        }
        string newKingdomId = null;
        if (payload.What.NewKingdom != null)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.NewKingdom, out newKingdomId)) return;
        }

        network.SendAll(new NetworkOnClanChangedKingdom(clanId, oldKingdomId, newKingdomId, payload.What.Detail));
    }

    private void HandleNetworkOnClanChangedKingdom(MessagePayload<NetworkOnClanChangedKingdom> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.ClanId, out var clan)) return;
            Kingdom oldKingdom = null;
            if (payload.What.OldKingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.OldKingdomId, out oldKingdom)) return;
            }
            Kingdom newKingdom = null;
            if (payload.What.NewKingdomId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.NewKingdomId, out newKingdom)) return;
            }

            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, oldKingdom, newKingdom, payload.What.Detail, true);
        });
    }

    private void HandleOnClanSupported(MessagePayload<OnClanSupported> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.SupporterClan, out var supporterClanId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.SupportedClan, out var supportedClanId)) return;

        network.SendAll(new NetworkOnClanSupported(supporterClanId, supportedClanId));
    }

    private void HandleNetworkOnClanSupported(MessagePayload<NetworkOnClanSupported> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.SupporterClanId, out var supporterClan)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.SupportedClanId, out var supportedClan)) return;
            DiplomacyModel diplomacyModel = Campaign.Current.Models.DiplomacyModel;
            int influenceCostOfSupportingClan = diplomacyModel.GetInfluenceCostOfSupportingClan();
            if (!(payload.Who is NetPeer peer))
            {
                Logger.Error("Received {Message} without a registered peer", nameof(NetworkOnClanSupported));
                return;
            }
            if (supporterClan.Influence >= (float)influenceCostOfSupportingClan)
            {
                int influenceValueOfSupportingClan = diplomacyModel.GetInfluenceValueOfSupportingClan();
                int relationValueOfSupportingClan = diplomacyModel.GetRelationValueOfSupportingClan();

                ChangeClanInfluenceAction.Apply(supporterClan, (float)(-(float)influenceCostOfSupportingClan));
                ChangeClanInfluenceAction.Apply(supportedClan, (float)influenceValueOfSupportingClan);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(supporterClan.Leader, supportedClan.Leader, relationValueOfSupportingClan, true);
                network.Send(peer, new NetworkOnClanSupportedApplied());
            }
        });
    }

    private void HandleNetworkOnClanSupportedApplied(MessagePayload<NetworkOnClanSupportedApplied> payload)
    {
        GameThread.RunSafe(() =>
        {
            var vm = KingdomClanVMPatch.Current;
            if (vm == null) return;

            Clan clan = vm.CurrentSelectedClan.Clan;
            vm.RefreshClan();
            vm.SelectClan(clan);
        });
    }
}
