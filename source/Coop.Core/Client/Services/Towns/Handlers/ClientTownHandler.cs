using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Towns.Messages;
using GameInterface.Services.Towns.Messages;

namespace Coop.Core.Client.Services.Towns.Handlers
{
    /// <summary>
    /// Handles Network Communications from the Server regarding town states.
    /// </summary>
    public class ClientTownHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientTownHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeTownLoyalty>(HandleTownLoyalty);
            messageBroker.Subscribe<NetworkChangeTownProsperity>(HandleTownProsperity);
            messageBroker.Subscribe<NetworkChangeTownSecurity>(HandleTownSecurity);
            messageBroker.Subscribe<NetworkChangeTownLastCapturedBy>(HandleTownLastCapturedBy);
            messageBroker.Subscribe<NetworkChangeTownInRebelliousState>(HandleTownInRebelliousState);
            messageBroker.Subscribe<NetworkChangeTownGovernor>(HandleTownGovernor);
            messageBroker.Subscribe<NetworkChangeTownGarrisonAutoRecruitmentIsEnabled>(HandleTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Subscribe<NetworkChangeTownTradeTaxAccumulated>(HandleTownTradeTaxAccumulated);
            messageBroker.Subscribe<NetworkChangeTownSoldItems>(HandleTownSoldItems);
            
            messageBroker.Subscribe<TownAuditorSent>(HandleTownAuditor);
        }

        private void HandleTownAuditor(MessagePayload<TownAuditorSent> obj)
        {
            TownAuditorSent townAuditorSent = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeTownAuditor networkChangeTownAuditor = new NetworkChangeTownAuditor(townAuditorSent.Datas);
            network.SendAll(networkChangeTownAuditor);
        }
        private void HandleTownSoldItems(MessagePayload<NetworkChangeTownSoldItems> payload)
        {
            NetworkChangeTownSoldItems networkChangeTownSoldItems = payload.What;
            ChangeTownSoldItems message = new ChangeTownSoldItems(networkChangeTownSoldItems.TownId, networkChangeTownSoldItems.LogList);
            messageBroker.Publish(this, message);
        }

        private void HandleTownTradeTaxAccumulated(MessagePayload<NetworkChangeTownTradeTaxAccumulated> payload)
        {
            NetworkChangeTownTradeTaxAccumulated networkChangeTownTradeTaxAccumulated = payload.What;
            ChangeTownTradeTaxAccumulated message = new ChangeTownTradeTaxAccumulated(networkChangeTownTradeTaxAccumulated.TownId, networkChangeTownTradeTaxAccumulated.TradeTaxAccumulated);
            messageBroker.Publish(this, message);
        }

        private void HandleTownGarrisonAutoRecruitmentIsEnabled(MessagePayload<NetworkChangeTownGarrisonAutoRecruitmentIsEnabled> payload)
        {
            NetworkChangeTownGarrisonAutoRecruitmentIsEnabled networkChangeTownGarrisonAutoRecruitmentIsEnabled = payload.What;
            ChangeTownGarrisonAutoRecruitmentIsEnabled message = new ChangeTownGarrisonAutoRecruitmentIsEnabled(networkChangeTownGarrisonAutoRecruitmentIsEnabled.TownId, networkChangeTownGarrisonAutoRecruitmentIsEnabled.GarrisonAutoRecruitmentIsEnabled);
            messageBroker.Publish(this, message);
        }

        private void HandleTownGovernor(MessagePayload<NetworkChangeTownGovernor> payload)
        {
            NetworkChangeTownGovernor networkChangeTownGovernor = payload.What;
            ChangeTownGovernor message = new ChangeTownGovernor(networkChangeTownGovernor.TownId, networkChangeTownGovernor.GovernorId);
            messageBroker.Publish(this, message);
        }

        private void HandleTownInRebelliousState(MessagePayload<NetworkChangeTownInRebelliousState> payload)
        {
            NetworkChangeTownInRebelliousState networkChangeTownInRebelliousState = payload.What;
            ChangeTownInRebelliousState message = new ChangeTownInRebelliousState(networkChangeTownInRebelliousState.TownId, networkChangeTownInRebelliousState.InRebelliousState);
            messageBroker.Publish(this, message);
        }

        private void HandleTownLastCapturedBy(MessagePayload<NetworkChangeTownLastCapturedBy> payload)
        {
            NetworkChangeTownLastCapturedBy networkChangeTownLastCapturedBy = payload.What;
            ChangeTownLastCapturedBy message = new ChangeTownLastCapturedBy(networkChangeTownLastCapturedBy.TownId, networkChangeTownLastCapturedBy.ClanId);
            messageBroker.Publish(this, message);
        }

        private void HandleTownSecurity(MessagePayload<NetworkChangeTownSecurity> payload)
        {
            NetworkChangeTownSecurity networkChangeTownSecurity = payload.What;
            ChangeTownSecurity message = new ChangeTownSecurity(networkChangeTownSecurity.TownId, networkChangeTownSecurity.Security);
            messageBroker.Publish(this, message);
        }

        private void HandleTownProsperity(MessagePayload<NetworkChangeTownProsperity> payload)
        {
            NetworkChangeTownProsperity networkChangeTownProsperity = payload.What;
            ChangeTownProsperity message = new ChangeTownProsperity(networkChangeTownProsperity.TownId, networkChangeTownProsperity.Prosperity);
            messageBroker.Publish(this, message);
        }

        private void HandleTownLoyalty(MessagePayload<NetworkChangeTownLoyalty> payload)
        {
            NetworkChangeTownLoyalty networkChangeTownLoyalty = payload.What;
            ChangeTownLoyalty message = new ChangeTownLoyalty(networkChangeTownLoyalty.TownId, networkChangeTownLoyalty.Loyalty);
            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeTownLoyalty>(HandleTownLoyalty);
            messageBroker.Unsubscribe<NetworkChangeTownProsperity>(HandleTownProsperity);
            messageBroker.Unsubscribe<NetworkChangeTownSecurity>(HandleTownSecurity);
            messageBroker.Unsubscribe<NetworkChangeTownLastCapturedBy>(HandleTownLastCapturedBy);
            messageBroker.Unsubscribe<NetworkChangeTownInRebelliousState>(HandleTownInRebelliousState);
            messageBroker.Unsubscribe<NetworkChangeTownGovernor>(HandleTownGovernor);
            messageBroker.Unsubscribe<NetworkChangeTownGarrisonAutoRecruitmentIsEnabled>(HandleTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Unsubscribe<NetworkChangeTownTradeTaxAccumulated>(HandleTownTradeTaxAccumulated);
            messageBroker.Unsubscribe<NetworkChangeTownSoldItems>(HandleTownSoldItems);
            messageBroker.Unsubscribe<TownAuditorSent>(HandleTownAuditor);
        }
    }
}