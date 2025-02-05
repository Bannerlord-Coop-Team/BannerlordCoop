using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Towns.Messages;
using GameInterface.Services.Towns.Messages;

namespace Coop.Core.Server.Services.Towns.Handlers
{
    /// <summary>
    /// Handles network related data for Towns
    /// </summary>
    public class ServerTownHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerTownHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

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
            NetworkChangeTownAuditor networkChangeTownAuditor = payload.What;
            SendTownAuditor message = new SendTownAuditor(networkChangeTownAuditor.Datas);
            messageBroker.Publish(this, message);
        }
        private void HandleTownSoldItems(MessagePayload<TownSoldItemsChanged> obj)
        {
            TownSoldItemsChanged townSoldItemsChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownSoldItems networkChangeTownSoldItems = new NetworkChangeTownSoldItems(townSoldItemsChanged.TownId, townSoldItemsChanged.LogList);
            network.SendAll(networkChangeTownSoldItems);
        }

        private void HandleTownGarrisonAutoRecruitmentIsEnabled(MessagePayload<TownGarrisonAutoRecruitmentIsEnabledChanged> obj)
        {
            TownGarrisonAutoRecruitmentIsEnabledChanged townGarrisonAutoRecruitmentIsEnabledChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownGarrisonAutoRecruitmentIsEnabled networkChangeTownGarrisonAutoRecruitmentIsEnabled = new NetworkChangeTownGarrisonAutoRecruitmentIsEnabled(townGarrisonAutoRecruitmentIsEnabledChanged.TownId, townGarrisonAutoRecruitmentIsEnabledChanged.GarrisonAutoRecruitmentIsEnabled);
            network.SendAll(networkChangeTownGarrisonAutoRecruitmentIsEnabled);
        }

        private void HandleTownLastCapturedBy(MessagePayload<TownLastCapturedByChanged> obj)
        {
            TownLastCapturedByChanged townLastCapturedByChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownLastCapturedBy networkChangeTownLastCapturedBy = new NetworkChangeTownLastCapturedBy(townLastCapturedByChanged.TownId, townLastCapturedByChanged.ClanId);
            network.SendAll(networkChangeTownLastCapturedBy);
        }

        private void HandleTownInRebelliousState(MessagePayload<TownInRebelliousStateChanged> obj)
        {
            TownInRebelliousStateChanged townInRebelliousStateChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownInRebelliousState networkChangeTownLastCapturedBy = new NetworkChangeTownInRebelliousState(townInRebelliousStateChanged.TownId, townInRebelliousStateChanged.InRebelliousState);
            network.SendAll(networkChangeTownLastCapturedBy);
        }

        private void HandleTownSecurity(MessagePayload<TownSecurityChanged> obj)
        {
            TownSecurityChanged townSecurityChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownSecurity networkChangeTownSecurity = new NetworkChangeTownSecurity(townSecurityChanged.TownId, townSecurityChanged.Security);
            network.SendAll(networkChangeTownSecurity);
        }

        private void HandleTownProsperity(MessagePayload<TownProsperityChanged> obj)
        {
            TownProsperityChanged townProsperityChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownProsperity networkChangeTownProsperity = new NetworkChangeTownProsperity(townProsperityChanged.TownId, townProsperityChanged.Prosperity);
            network.SendAll(networkChangeTownProsperity);
        }

        private void HandleTownLoyalty(MessagePayload<TownLoyaltyChanged> obj)
        {
            TownLoyaltyChanged townLoyaltyChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownLoyalty networkChangeTownLoyalty = new NetworkChangeTownLoyalty(townLoyaltyChanged.TownId, townLoyaltyChanged.Loyalty);
            network.SendAll(networkChangeTownLoyalty);
        }

        private void HandleTownTradeTaxAccumulated(MessagePayload<TownTradeTaxAccumulatedChanged> obj)
        {
            TownTradeTaxAccumulatedChanged townTradeTaxAccumulatedChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownTradeTaxAccumulated networkChangeTownTradeTaxAccumulated = new NetworkChangeTownTradeTaxAccumulated(townTradeTaxAccumulatedChanged.TownId, townTradeTaxAccumulatedChanged.TradeTaxAccumulated);
            network.SendAll(networkChangeTownTradeTaxAccumulated);
        }

        private void HandleTownGovernor(MessagePayload<TownGovernorChanged> obj)
        {
            TownGovernorChanged townGovernorChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownGovernor networkChangeTownGovernor = new NetworkChangeTownGovernor(townGovernorChanged.TownId, townGovernorChanged.GovernorId);
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