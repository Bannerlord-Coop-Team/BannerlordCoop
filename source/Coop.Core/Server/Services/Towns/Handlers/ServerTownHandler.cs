using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.Towns.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;

namespace Coop.Core.Server.Services.Towns.Handlers
{
    /// <summary>
    /// Handles network related data for Towns
    /// </summary>
    public class ServerTownHandler : IHandler
    {
        // Coalescer channel for per-town trade-tax updates; only the latest accumulated value per town is sent each tick.
        private const string TownTradeTaxAccumulatedChannel = "TownTradeTaxAccumulated";

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ISendCoalescer coalescer;

        public ServerTownHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISendCoalescer coalescer)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            this.coalescer = coalescer;

            // This handles an internal message
            messageBroker.Subscribe<TownGovernorChanged>(HandleTownGovernor);
            messageBroker.Subscribe<TownTradeTaxAccumulatedChanged>(HandleTownTradeTaxAccumulated);
            messageBroker.Subscribe<TownLoyaltyChanged>(HandleTownLoyalty);
            messageBroker.Subscribe<TownProsperityChanged>(HandleTownProsperity);
            messageBroker.Subscribe<TownSecurityChanged>(HandleTownSecurity);
            messageBroker.Subscribe<TownInRebelliousStateChanged>(HandleTownInRebelliousState);
            messageBroker.Subscribe<TownLastCapturedByChanged>(HandleTownLastCapturedBy);
            messageBroker.Subscribe<TownGarrisonAutoRecruitmentIsEnabledChanged>(HandleTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Subscribe<TownSoldItemsChanged>(HandleTownSoldItems);
            messageBroker.Subscribe<NetworkChangeTownAuditor>(HandleTownAuditorSent);
        }

        private void HandleTownAuditorSent(MessagePayload<NetworkChangeTownAuditor> payload)
        {
            var networkChangeTownAuditor = payload.What;
            var message = new SendTownAuditor(networkChangeTownAuditor.AuditData);

            messageBroker.Publish(this, message);
        }

        private void HandleTownSoldItems(MessagePayload<TownSoldItemsChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownSoldItems = new NetworkChangeTownSoldItems(townId, payload.LogList);
            network.SendAll(networkChangeTownSoldItems);
        }

        private void HandleTownGarrisonAutoRecruitmentIsEnabled(MessagePayload<TownGarrisonAutoRecruitmentIsEnabledChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownGarrisonAutoRecruitmentIsEnabled =
                new NetworkChangeTownGarrisonAutoRecruitmentIsEnabled(
                    townId,
                    payload.GarrisonAutoRecruitmentIsEnabled);

            network.SendAll(networkChangeTownGarrisonAutoRecruitmentIsEnabled);
        }

        private void HandleTownLastCapturedBy(MessagePayload<TownLastCapturedByChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(payload.Clan, out var clanId)) return;

            var networkChangeTownLastCapturedBy = new NetworkChangeTownLastCapturedBy(townId, clanId);
            network.SendAll(networkChangeTownLastCapturedBy);
        }

        private void HandleTownInRebelliousState(MessagePayload<TownInRebelliousStateChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownInRebelliousState =
                new NetworkChangeTownInRebelliousState(townId, payload.InRebelliousState);

            network.SendAll(networkChangeTownInRebelliousState);
        }

        private void HandleTownSecurity(MessagePayload<TownSecurityChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownSecurity = new NetworkChangeTownSecurity(townId, payload.Security);
            network.SendAll(networkChangeTownSecurity);
        }

        private void HandleTownProsperity(MessagePayload<TownProsperityChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownProsperity = new NetworkChangeTownProsperity(townId, payload.Prosperity);
            network.SendAll(networkChangeTownProsperity);
        }

        private void HandleTownLoyalty(MessagePayload<TownLoyaltyChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownLoyalty = new NetworkChangeTownLoyalty(townId, payload.Loyalty);
            network.SendAll(networkChangeTownLoyalty);
        }

        private void HandleTownTradeTaxAccumulated(MessagePayload<TownTradeTaxAccumulatedChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            var networkChangeTownTradeTaxAccumulated =
                new NetworkChangeTownTradeTaxAccumulated(townId, payload.TradeTaxAccumulated);

            // Coalesce per town: repeated trade-tax changes to one town collapse into a single latest-wins send per tick.
            var key = new CoalesceKey(TownTradeTaxAccumulatedChannel, townId);
            coalescer.Enqueue(key, new LatestWinsPayload(networkChangeTownTradeTaxAccumulated));
        }

        private void HandleTownGovernor(MessagePayload<TownGovernorChanged> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Town, out var townId)) return;

            string? governorId = null;
            if (payload.Governor != null &&
                !objectManager.TryGetIdWithLogging(payload.Governor, out governorId))
            {
                return;
            }

            var networkChangeTownGovernor = new NetworkChangeTownGovernor(townId, governorId);
            network.SendAll(networkChangeTownGovernor);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TownGovernorChanged>(HandleTownGovernor);
            messageBroker.Unsubscribe<TownTradeTaxAccumulatedChanged>(HandleTownTradeTaxAccumulated);
            messageBroker.Unsubscribe<TownLoyaltyChanged>(HandleTownLoyalty);
            messageBroker.Unsubscribe<TownProsperityChanged>(HandleTownProsperity);
            messageBroker.Unsubscribe<TownSecurityChanged>(HandleTownSecurity);
            messageBroker.Unsubscribe<TownInRebelliousStateChanged>(HandleTownInRebelliousState);
            messageBroker.Unsubscribe<TownLastCapturedByChanged>(HandleTownLastCapturedBy);
            messageBroker.Unsubscribe<TownGarrisonAutoRecruitmentIsEnabledChanged>(HandleTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Unsubscribe<TownSoldItemsChanged>(HandleTownSoldItems);
            messageBroker.Unsubscribe<NetworkChangeTownAuditor>(HandleTownAuditorSent);
        }
    }
}