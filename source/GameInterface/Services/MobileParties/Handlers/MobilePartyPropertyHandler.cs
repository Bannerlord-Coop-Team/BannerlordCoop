using Autofac.Core;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Handlers
{
    public class MobilePartyPropertyHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyFieldsHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public MobilePartyPropertyHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeMobilePartyProperty>(Handle);
        }

        private void Handle(MessagePayload<ChangeMobilePartyProperty> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<MobileParty>(data.Value1, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value1);
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {

                    switch ((PropertyType)data.PropertyType)
                    {
                        case PropertyType.Army:
                            if(data.Value2 == null)
                            {
                                instance.Army = null;
                                return;
                            }
                            if (objectManager.TryGetObject<Army>(data.Value2, out var army) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Army), data.Value2);
                                return;
                            }
                            instance.Army = army;
                            break;

                        case PropertyType.CustomName:
                            instance.CustomName = new TextObject(data.Value2);
                            break;

                        case PropertyType.LastVisitedSettlement:
                            if (objectManager.TryGetObject<Settlement>(data.Value2, out var settlement) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.Value2);
                                return;
                            }
                            instance.LastVisitedSettlement = settlement;
                            break;

                        case PropertyType.Aggressiveness:
                            instance.Aggressiveness = float.Parse(data.Value2);
                            break;

                        case PropertyType.ArmyPositionAdder:
                            instance.ArmyPositionAdder = new Vec2(float.Parse(data.Value2), float.Parse(data.Value3));
                            break;

                        case PropertyType.Objective:
                            instance.Objective = (MobileParty.PartyObjective)int.Parse(data.Value2);
                            break;

                        case PropertyType.Ai:
                            if (objectManager.TryGetObject<MobileParty>(data.Value2, out var mobilePartyForAi) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value2);
                                return;
                            }
                            instance.Ai = new MobilePartyAi(mobilePartyForAi);
                            break;

                        case PropertyType.Party:
                            if (objectManager.TryGetObject<MobileParty>(data.Value2, out var party) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value2);
                                return;
                            }
                            instance.Party = party.Party;
                            break;

                        case PropertyType.IsActive:
                            instance.IsActive = bool.Parse(data.Value2);
                            break;

                        case PropertyType.ThinkParamsCache:
                            //TODO
                            break;

                        case PropertyType.ShortTermBehaviour:
                            instance.ShortTermBehavior = (AiBehavior)Enum.Parse(typeof(AiBehavior), data.Value2);
                            break;

                        case PropertyType.IsPartyTradeActive:
                            instance.IsPartyTradeActive = bool.Parse(data.Value2);
                            break;

                        case PropertyType.PartyTradeGold:
                            instance.PartyTradeGold = int.Parse(data.Value2);
                            break;

                        case PropertyType.PartyTradeTaxGold:
                            instance.PartyTradeTaxGold = int.Parse(data.Value2);
                            break;

                        case PropertyType.StationaryStartTime:
                            instance.StationaryStartTime = new CampaignTime(long.Parse(data.Value2));
                            break;

                        case PropertyType.VersionNo:
                            instance.VersionNo = int.Parse(data.Value2);
                            break;

                        case PropertyType.ShouldJoinPlayerBattles:
                            instance.ShouldJoinPlayerBattles = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsDisbanding:
                            instance.IsDisbanding = bool.Parse(data.Value2);
                            break;

                        case PropertyType.CurrentSettlement:
                            if (objectManager.TryGetObject<Settlement>(data.Value2, out var curSettlement) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.Value2);
                                return;
                            }
                            instance.CurrentSettlement = curSettlement;
                            break;

                        case PropertyType.AttachedTo:
                            if (objectManager.TryGetObject<MobileParty>(data.Value2, out var attachedToParty) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value2);
                                return;
                            }
                            instance.AttachedTo = attachedToParty;
                            break;

                        case PropertyType.BesiegerCamp:
                            //TODO
                            break;

                        case PropertyType.Scout:
                            if (data.Value2 == null)
                            {
                                instance.Scout = null;
                                break;
                            }
                            if (objectManager.TryGetObject<Hero>(data.Value2, out var scout) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                                return;
                            }
                            instance.Scout = scout;
                            break;

                        case PropertyType.Engineer:
                            if (objectManager.TryGetObject<Hero>(data.Value2, out var engineer) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                                return;
                            }
                            instance.Engineer = engineer;
                            break;

                        case PropertyType.Quartermaster:
                            if (data.Value2 == null)
                            {
                                instance.Quartermaster = null;
                                break;
                            }
                            if (objectManager.TryGetObject<Hero>(data.Value2, out var quatermaster) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                                return;
                            }
                            instance.Quartermaster = quatermaster;
                            break;

                        case PropertyType.Surgeon:
                            if(data.Value2 == null)
                            {
                                instance.Surgeon = null; 
                                break;
                            }
                            if (objectManager.TryGetObject<Hero>(data.Value2, out var surgeon) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                                return;
                            }
                            instance.Surgeon = surgeon;
                            break;

                        case PropertyType.ActualClan:
                            if(data.Value2 == null)
                            {
                                instance.ActualClan = null;
                                return;
                            }
                            if (objectManager.TryGetObject<Clan>(data.Value2, out var actualClan) == false)
                            {
                                Logger.Error("Unable to find {type} with id: {id}", typeof(Clan), data.Value2);
                                return;
                            }
                            instance.ActualClan = actualClan;
                            break;

                        case PropertyType.RecentEventsMorale:
                            instance.RecentEventsMorale = float.Parse(data.Value2);
                            break;

                        case PropertyType.EventPositionAdder:
                            instance.EventPositionAdder = new Vec2(float.Parse(data.Value2), float.Parse(data.Value3));
                            break;

                        case PropertyType.IsInspected:
                            instance.IsInspected = bool.Parse(data.Value2);
                            break;

                        case PropertyType.MapEventSide:
                            //TODO
                            break;

                        case PropertyType.PartyComponent:
                            //TODO
                            break;

                        case PropertyType.IsMilita:
                            instance.IsMilitia = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsLordParty:
                            instance.IsLordParty = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsVillager:
                            instance.IsVillager = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsCaravan:
                            instance.IsCaravan = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsGarrison:
                            instance.IsGarrison = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsCustomParty:
                            instance.IsCustomParty = bool.Parse(data.Value2);
                            break;

                        case PropertyType.IsBandit:
                            instance.IsBandit = bool.Parse(data.Value2);
                            break;
                    }
                }
            });
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeMobilePartyProperty>(Handle);
        }
    }
}
