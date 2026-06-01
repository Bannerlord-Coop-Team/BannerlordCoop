using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Banners.Handlers
{
    /// <summary>
    /// Handles propagation of player banner edits across the network.
    /// </summary>
    public class PlayerBannerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<PlayerBannerHandler>();

        public PlayerBannerHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<PlayerBannerChanged>(Handle);
            messageBroker.Subscribe<NetworkUpdatePlayerBanner>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PlayerBannerChanged>(Handle);
            messageBroker.Unsubscribe<NetworkUpdatePlayerBanner>(Handle);
        }

        /// <summary>
        /// Local edit by this player: forward to the network.
        /// </summary>
        private void Handle(MessagePayload<PlayerBannerChanged> obj)
        {
            var payload = obj.What;
            network.SendAll(new NetworkUpdatePlayerBanner(payload.BannerCode, payload.ClanId, payload.Color, payload.Color2));
        }

        /// <summary>
        /// Incoming banner update from the network: apply it locally, and (if server) relay to all clients.
        /// </summary>
        private void Handle(MessagePayload<NetworkUpdatePlayerBanner> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var clan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    if (clan.Banner == null)
                    {
                        clan.Banner = new Banner(payload.BannerCode);
                    }
                    else
                    {
                        clan.Banner.Deserialize(payload.BannerCode);
                    }

                    clan.Color = payload.Color;
                    clan.Color2 = payload.Color2;
                    clan.UpdateBannerColor(payload.Color, payload.Color2);

                    // The banner is mutated in place, so the cached party map visuals (the flag rendered
                    // on the campaign map) won't rebuild on their own. Mark every party belonging to the
                    // clan as visually dirty so the visual is regenerated from the new banner code.
                    foreach (var warParty in clan.WarPartyComponents)
                    {
                        warParty?.MobileParty?.Party?.SetVisualAsDirty();
                    }

                    // The small banner flag on each party's map nameplate is a separate UI cache that
                    // only rebuilds when the nameplate is flagged dirty. There is no vanilla event for a
                    // banner edit, so force the affected nameplates to refresh their banner here.
                    RefreshClanNameplateBanners(clan);
                }
            });

            if (ModInformation.IsServer)
            {
                network.SendAll(new NetworkUpdatePlayerBanner(payload.BannerCode, payload.ClanId, payload.Color, payload.Color2));
            }
        }

        /// <summary>
        /// Forces the map nameplates of every party belonging to <paramref name="clan"/> to rebuild
        /// their cached banner image. No-op when the campaign map UI is not currently active
        /// (e.g. on a headless server).
        /// </summary>
        private void RefreshClanNameplateBanners(Clan clan)
        {
            var nameplateView = MapScreen.Instance?.GetMapView<GauntletMapPartyNameplateView>();
            var dataSource = nameplateView?._dataSource;
            if (dataSource == null) return;

            RefreshNameplateIfClanMatches(dataSource.PlayerNameplate, clan);

            foreach (var nameplate in dataSource.Nameplates)
            {
                RefreshNameplateIfClanMatches(nameplate, clan);
            }
        }

        private static void RefreshNameplateIfClanMatches(PartyNameplateVM nameplate, Clan clan)
        {
            if (nameplate?.Party?.LeaderHero?.Clan == clan)
            {
                nameplate.RefreshValues();
            }
        }
    }
}
