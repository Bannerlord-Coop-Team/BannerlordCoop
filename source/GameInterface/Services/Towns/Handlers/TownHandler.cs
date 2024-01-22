using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Handlers
{
    /// <summary>
    /// Handles TownState Changes (e.g. Prosperity, Governor, etc.).
    /// </summary>
    public class TownHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TownHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public TownHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<ChangeTownGovernor>(HandleChangeTownGovernor);
            messageBroker.Subscribe<ChangeTownLoyalty>(HandleChangeTownLoyalty);
            messageBroker.Subscribe<ChangeTownProsperity>(HandleChangeTownProsperity);
            messageBroker.Subscribe<ChangeTownSecurity>(HandleChangeTownSecurity);
            messageBroker.Subscribe<ChangeTownLastCapturedBy>(HandleChangeTownLastCapturedBy);
            messageBroker.Subscribe<ChangeTownInRebelliousState>(HandleChangeTownInRebelliousState);
            messageBroker.Subscribe<ChangeTownGarrisonAutoRecruitmentIsEnabled>(HandleChangeTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Subscribe<ChangeTownTradeTaxAccumulated>(HandleChangeTownTradeTaxAccumulated);
        }

        private void HandleChangeTownTradeTaxAccumulated(MessagePayload<ChangeTownTradeTaxAccumulated> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownGarrisonAutoRecruitmentIsEnabled(MessagePayload<ChangeTownGarrisonAutoRecruitmentIsEnabled> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownInRebelliousState(MessagePayload<ChangeTownInRebelliousState> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownLastCapturedBy(MessagePayload<ChangeTownLastCapturedBy> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownSecurity(MessagePayload<ChangeTownSecurity> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownProsperity(MessagePayload<ChangeTownProsperity> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownLoyalty(MessagePayload<ChangeTownLoyalty> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        private void HandleChangeTownGovernor(MessagePayload<ChangeTownGovernor> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject<Town>(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeTownGovernor>(HandleChangeTownGovernor);
            messageBroker.Unsubscribe<ChangeTownLoyalty>(HandleChangeTownLoyalty);
            messageBroker.Unsubscribe<ChangeTownProsperity>(HandleChangeTownProsperity);
            messageBroker.Unsubscribe<ChangeTownSecurity>(HandleChangeTownSecurity);
            messageBroker.Unsubscribe<ChangeTownLastCapturedBy>(HandleChangeTownLastCapturedBy);
            messageBroker.Unsubscribe<ChangeTownInRebelliousState>(HandleChangeTownInRebelliousState);
            messageBroker.Unsubscribe<ChangeTownGarrisonAutoRecruitmentIsEnabled>(HandleChangeTownGarrisonAutoRecruitmentIsEnabled);
            messageBroker.Unsubscribe<ChangeTownTradeTaxAccumulated>(HandleChangeTownTradeTaxAccumulated);
        }
    }
}
