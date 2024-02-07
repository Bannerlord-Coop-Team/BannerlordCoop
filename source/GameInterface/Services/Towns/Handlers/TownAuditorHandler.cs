using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Data;
using GameInterface.Services.Towns.Messages;
using GameInterface.Services.Towns.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Towns.Handlers
{
    /// <summary>
    /// Handles Town Auditor (send all sync).
    /// </summary>
    public class TownAuditorHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TownAuditorHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private static readonly Func<Town, Town.SellLog[]> getSoldItems = typeof(Town).GetField("_soldItems", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Town, Town.SellLog[]>();
        public TownAuditorHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<SendTownAuditor>(HandleSendTownAuditor);
        }

        private void HandleSendTownAuditor(MessagePayload<SendTownAuditor> payload)
        {
            var obj = payload.What;
            var clientTownAuditorDatas = obj.Datas;

            List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown).ToList();

            List<TownAuditorData> serverTownAuditorDatas = new List<TownAuditorData>();
            settlements.ForEach((settlement) =>
            {

                if (objectManager.TryGetObject(settlement.Town.StringId, out Town t) == false)
                {
                    Logger.Error("TownAuditorHandler: Town not found on server: " + settlement.Town.StringId);
                }
                else
                {
                    Fief fief = t.Settlement.SettlementComponent as Fief;

                    TownAuditorData auditorData = new TownAuditorData(
                        t.StringId, t.Name.ToString(), (t.Governor != null) ? t.Governor.Name.ToString() : "null",
                        (t.LastCapturedBy != null) ? t.LastCapturedBy.Name.ToString() : "null",
                        t.Prosperity, t.Loyalty, t.Security, t.InRebelliousState, t.GarrisonAutoRecruitmentIsEnabled,
                       fief.FoodStocks, t.TradeTaxAccumulated, getSoldItems(t));

                    serverTownAuditorDatas.Add(auditorData);
                }


            });

            // Compare client and server values
            foreach (var clientTownAuditorData in clientTownAuditorDatas)
            {
                var serverTownAuditorData = serverTownAuditorDatas.FirstOrDefault(x => x.TownStringId == clientTownAuditorData.TownStringId);
                if (serverTownAuditorData == null)
                {
                    Logger.Error("TownAuditorHandler: Town not found on server: " + clientTownAuditorData.TownStringId);
                    continue;
                }

                if (clientTownAuditorData.Name != serverTownAuditorData.Name)
                {
                    Logger.Error("TownAuditorHandler: Name mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.Governor != serverTownAuditorData.Governor)
                {
                    Logger.Error("TownAuditorHandler: Governor mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.LastCapturedBy != serverTownAuditorData.LastCapturedBy)
                {
                    Logger.Error("TownAuditorHandler: LastCapturedBy mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.Prosperity != serverTownAuditorData.Prosperity)
                {
                    Logger.Error("TownAuditorHandler: Prosperity mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.Loyalty != serverTownAuditorData.Loyalty)
                {
                    Logger.Error("TownAuditorHandler: Loyalty mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.Security != serverTownAuditorData.Security)
                {
                    Logger.Error("TownAuditorHandler: Security mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.InRebelliousState != serverTownAuditorData.InRebelliousState)
                {
                    Logger.Error("TownAuditorHandler: InRebelliousState mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                if (clientTownAuditorData.GarrisonAutoRecruitmentIsEnabled != serverTownAuditorData.GarrisonAutoRecruitmentIsEnabled)
                {
                    Logger.Error("TownAuditorHandler: Garrison");
                }
                
                if (clientTownAuditorData.FoodStocks != serverTownAuditorData.FoodStocks)
                {
                    Logger.Error("TownAuditorHandler: FoodStocks mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                
                if (clientTownAuditorData.SellLogList.Length != serverTownAuditorData.SellLogList.Length)
                {
                    Logger.Error("TownAuditorHandler: SellLogList length mismatch for town: " + clientTownAuditorData.TownStringId);
                }
                else
                {
                    foreach(var clientSellLog in clientTownAuditorData.SellLogList)
                    {
                        var serverSellLog = serverTownAuditorData.SellLogList.FirstOrDefault(x => x.CategoryID == clientSellLog.CategoryID);
                        if (serverSellLog == null)
                        {
                            Logger.Error("TownAuditorHandler: SellLog of category" + clientSellLog.CategoryID + " not found on server for town: " + clientTownAuditorData.TownStringId);
                            continue;
                        }
                        if (clientSellLog.CategoryID != serverSellLog.CategoryID)
                        {
                            Logger.Error("TownAuditorHandler: SellLog category ID mismatch for town: " + clientTownAuditorData.TownStringId);
                        }
                        if(clientSellLog.Number != serverSellLog.Number)
                        {
                            Logger.Error("TownAuditorHandler: SellLog number mismatch for town: " + clientTownAuditorData.TownStringId);
                        }
                    }
                }
            }

        }
        public void Dispose()
        {
        }
    }
}