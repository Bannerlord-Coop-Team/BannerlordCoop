using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;
using GameInterface.Services.Towns.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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
            messageBroker.Subscribe<ChangeTownSoldItems>(HandleChangeTownSoldItems);
        }

        private void HandleChangeTownSoldItems(MessagePayload<ChangeTownSoldItems> payload)
        {
            var obj = payload.What;

            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            var sellLogs = obj.LogList.Select(log =>
            {
                if (objectManager.TryGetObject(log.CategoryID, out ItemCategory itemCategory) == false)
                {
                    Logger.Error("Unable to find ItemCategory ({itemCategoryId})", log.CategoryID);
                    return default;
                }
                return new Town.SellLog(itemCategory, log.Number);
            }).Where(log => log.Equals(default) == false);

            TownPatches.ChangeSetSoldItems(town, sellLogs);
        }

        private void HandleChangeTownTradeTaxAccumulated(MessagePayload<ChangeTownTradeTaxAccumulated> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTradeTaxAccumulated(town, obj.TradeTaxAccumulated);
        }

        private void HandleChangeTownGarrisonAutoRecruitmentIsEnabled(MessagePayload<ChangeTownGarrisonAutoRecruitmentIsEnabled> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTownGarrisonAutoRecruitmentIsEnabled(town, obj.GarrisonAutoRecruitmentIsEnabled);
        }

        private void HandleChangeTownInRebelliousState(MessagePayload<ChangeTownInRebelliousState> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTownInRebelliousState(town, obj.InRebelliousState);
        }

        private void HandleChangeTownLastCapturedBy(MessagePayload<ChangeTownLastCapturedBy> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
            if (objectManager.TryGetObject(obj.ClanId, out Clan clan) == false)
            {
                Logger.Error("Unable to find Clan ({clanId})", obj.ClanId);
                return;
            }

            TownPatches.ChangeTownLastCapturedBy(town, clan);
        }

        private void HandleChangeTownSecurity(MessagePayload<ChangeTownSecurity> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTownSecurity(town, obj.Security);
        }

        private void HandleChangeTownProsperity(MessagePayload<ChangeTownProsperity> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTownProsperity(town, obj.Prosperity);
        }

        private void HandleChangeTownLoyalty(MessagePayload<ChangeTownLoyalty> payload)
        {
            var obj = payload.What;


            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }

            TownPatches.ChangeTownLoyalty(town, obj.Loyalty);
        }

        private void HandleChangeTownGovernor(MessagePayload<ChangeTownGovernor> payload)
        {
            var obj = payload.What;
            Hero governor = null;

            if (objectManager.TryGetObject(obj.TownId, out Town town) == false)
            {
                Logger.Error("Unable to find Town ({townId})", obj.TownId);
                return;
            }
            if (obj.GovernorId != null && objectManager.TryGetObject(obj.GovernorId, out governor) == false)
            {
                Logger.Error("Unable to find Hero ({governorId})", obj.GovernorId);
                return;
            }

            TownPatches.ChangeTownGovernor(town, governor);
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
            messageBroker.Unsubscribe<ChangeTownSoldItems>(HandleChangeTownSoldItems);
        }
    }
}