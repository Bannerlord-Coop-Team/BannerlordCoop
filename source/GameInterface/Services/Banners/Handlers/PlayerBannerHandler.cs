using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
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
        private readonly IPlayerManager playerManager;
        private readonly ICoopClanPermissions permissions;
        private readonly ILogger Logger = LogManager.GetLogger<PlayerBannerHandler>();

        public PlayerBannerHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IPlayerManager playerManager,
            ICoopClanPermissions permissions)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.playerManager = playerManager;
            this.permissions = permissions;
            messageBroker.Subscribe<PlayerBannerChanged>(Handle);
            messageBroker.Subscribe<NetworkUpdatePlayerBanner>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PlayerBannerChanged>(Handle);
            messageBroker.Unsubscribe<NetworkUpdatePlayerBanner>(Handle);
        }

        /// <summary>
        /// Local edit by this player: resolve the clan's network id and forward to the network.
        /// </summary>
        private void Handle(MessagePayload<PlayerBannerChanged> obj)
        {
            var clan = obj.What.Clan;

            if (clan == null) return;

            if (objectManager.TryGetId(clan, out string clanId) == false)
            {
                Logger.Error("Unable to resolve network id for clan {clanName}", clan.Name);
                return;
            }

            network.SendAll(new NetworkUpdatePlayerBanner(clan.Banner.Serialize(), clanId, clan.Color, clan.Color2));
            messageBroker.Publish(this, new ClanManagementChanged(clan, ClanManagementRefresh.Identity));
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

            // Only the clan leader may edit the banner, same rule as the banner editor button.
            if (ModInformation.IsServer && !CanManageClan(obj.Who, clan))
            {
                Logger.Warning("Ignoring banner edit for clan {clanId} from a player who does not lead it", payload.ClanId);
                return;
            }

            GameThread.Run(() =>
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

                    // A ruler's own banner is kept apart from the kingdom banner and shows again once
                    // they lose the throne, so it takes the new colours on every instance too.
                    if (clan.Kingdom?.RulingClan == clan)
                    {
                        clan.ClanOriginalBanner?.ChangePrimaryColor(clan.Banner.GetPrimaryColor());
                        clan.ClanOriginalBanner?.ChangeIconColors(clan.Banner.GetFirstIconColor());
                    }

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

                if (ModInformation.IsServer)
                {
                    UpdateKingdomBannerColors(clan);
                }
            });

            if (ModInformation.IsServer)
            {
                network.SendAll(new NetworkUpdatePlayerBanner(payload.BannerCode, payload.ClanId, payload.Color, payload.Color2));
                messageBroker.Publish(this, new ClanManagementChanged(clan, ClanManagementRefresh.Identity));
            }
        }

        /// <summary>
        /// Stores a ruler's edited banner colours on the kingdom, otherwise every save load repaints the
        /// kingdom's old colours through <see cref="Clan.UpdateBannerColorsAccordingToKingdom"/>.
        /// </summary>
        private void UpdateKingdomBannerColors(Clan clan)
        {
            var kingdom = clan.Kingdom;
            if (kingdom?.RulingClan != clan) return;

            // Patches stay live here so the AutoSync setters replicate the colours to every client.
            kingdom.PrimaryBannerColor = clan.Banner.GetPrimaryColor();
            kingdom.SecondaryBannerColor = clan.Banner.GetFirstIconColor();

            // Repaint the member clans now instead of on the next load.
            foreach (var kingdomClan in kingdom.Clans)
            {
                kingdomClan.UpdateBannerColorsAccordingToKingdom();

                if (kingdomClan != clan)
                {
                    messageBroker.Publish(this, new PlayerBannerChanged(kingdomClan));
                }
            }
        }

        private bool CanManageClan(object sender, Clan clan)
        {
            return sender is NetPeer peer && playerManager.TryGetPlayer(peer, out var player) &&
                objectManager.TryGetObject<Hero>(player.HeroId, out var actor) &&
                permissions.CanManageClan(actor, clan);
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
