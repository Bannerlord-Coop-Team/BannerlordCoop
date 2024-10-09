using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
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
            if (objectManager.TryGetObject<MobileParty>(data.PartyId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.PartyId);
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    HandleDataChanged(instance, data);
                }
            });
        }

        private void HandleDataChanged(MobileParty instance, ChangeMobilePartyProperty data)
        {
            var propertyType = (PropertyType)data.PropertyType;

            switch (propertyType)
            {
                case PropertyType.Army:
                    if (data.Value2 == null)
                    {
                        instance._army = null;
                        return;
                    }
                    if (objectManager.TryGetObject<Army>(data.Value2, out var army) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Army), data.Value2);
                        return;
                    }
                    instance._army = army;
                    return;

                case PropertyType.CustomName:
                    instance.CustomName = new TextObject(data.Value2);
                    return;

                case PropertyType.LastVisitedSettlement:
                    if (objectManager.TryGetObject<Settlement>(data.Value2, out var settlement) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.Value2);
                        return;
                    }
                    instance.LastVisitedSettlement = settlement;
                    return;

                case PropertyType.Aggressiveness:
                    instance.Aggressiveness = float.Parse(data.Value2);
                    return;

                case PropertyType.Objective:
                    instance.Objective = (MobileParty.PartyObjective)int.Parse(data.Value2);
                    return;

                // Moved to autosync
                //case PropertyType.Ai:
                //    if (objectManager.TryGetObject<MobileParty>(data.Value2, out var mobilePartyForAi) == false)
                //    {
                //        Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value2);
                //        return;
                //    }
                //    instance.Ai = new MobilePartyAi(mobilePartyForAi);
                //    return;

                case PropertyType.IsActive:
                    instance.IsActive = bool.Parse(data.Value2);
                    return;

                case PropertyType.ShortTermBehaviour:
                    instance.ShortTermBehavior = (AiBehavior)Enum.Parse(typeof(AiBehavior), data.Value2);
                    return;

                case PropertyType.IsPartyTradeActive:
                    instance.IsPartyTradeActive = bool.Parse(data.Value2);
                    return;

                case PropertyType.PartyTradeGold:
                    instance.PartyTradeGold = int.Parse(data.Value2);
                    return;

                case PropertyType.PartyTradeTaxGold:
                    instance.PartyTradeTaxGold = int.Parse(data.Value2);
                    return;

                case PropertyType.StationaryStartTime:
                    instance.StationaryStartTime = new CampaignTime(long.Parse(data.Value2));
                    return;

                case PropertyType.VersionNo:
                    instance.VersionNo = int.Parse(data.Value2);
                    return;

                case PropertyType.ShouldJoinPlayerBattles:
                    instance.ShouldJoinPlayerBattles = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsDisbanding:
                    instance.IsDisbanding = bool.Parse(data.Value2);
                    return;

                case PropertyType.CurrentSettlement:
                    if (objectManager.TryGetObject<Settlement>(data.Value2, out var curSettlement) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.Value2);
                        return;
                    }
                    instance._currentSettlement = curSettlement;
                    return;

                case PropertyType.AttachedTo:
                    if (objectManager.TryGetObject<MobileParty>(data.Value2, out var attachedToParty) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), data.Value2);
                        return;
                    }
                    instance.AttachedTo = attachedToParty;
                    return;

                case PropertyType.BesiegerCamp:
                    if (data.Value2 == null)
                    {
                        instance._besiegerCamp = null;
                        return;
                    }
                    if (objectManager.TryGetObject<BesiegerCamp>(data.Value2, out var besiegerCamp) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), data.Value2);
                        return;
                    }

                    instance._besiegerCamp = besiegerCamp;
                    return;

                case PropertyType.Scout:
                    if (data.Value2 == null)
                    {
                        instance.Scout = null;
                        return;
                    }
                    if (objectManager.TryGetObject<Hero>(data.Value2, out var scout) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                        return;
                    }
                    instance.Scout = scout;
                    return;

                case PropertyType.Engineer:
                    if (objectManager.TryGetObject<Hero>(data.Value2, out var engineer) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                        return;
                    }
                    instance.Engineer = engineer;
                    return;

                case PropertyType.Quartermaster:
                    if (data.Value2 == null)
                    {
                        instance.Quartermaster = null;
                        return;
                    }
                    if (objectManager.TryGetObject<Hero>(data.Value2, out var quatermaster) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                        return;
                    }
                    instance.Quartermaster = quatermaster;
                    return;

                case PropertyType.Surgeon:
                    if (data.Value2 == null)
                    {
                        instance.Surgeon = null;
                        return;
                    }
                    if (objectManager.TryGetObject<Hero>(data.Value2, out var surgeon) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.Value2);
                        return;
                    }
                    instance.Surgeon = surgeon;
                    return;

                case PropertyType.ActualClan:
                    if (data.Value2 == null)
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
                    return;

                case PropertyType.RecentEventsMorale:
                    instance.RecentEventsMorale = float.Parse(data.Value2);
                    return;

                case PropertyType.EventPositionAdder:
                    instance.EventPositionAdder = new Vec2(float.Parse(data.Value2), float.Parse(data.Value3));
                    return;

                case PropertyType.PartyComponent:
                    if (objectManager.TryGetObject<PartyComponent>(data.Value2, out var partyComponent) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(PartyComponent), data.Value2);
                        return;
                    }
                    instance.PartyComponent = partyComponent;
                    return;

                case PropertyType.IsMilita:
                    instance.IsMilitia = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsLordParty:
                    instance.IsLordParty = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsVillager:
                    instance.IsVillager = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsCaravan:
                    instance.IsCaravan = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsGarrison:
                    instance.IsGarrison = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsCustomParty:
                    instance.IsCustomParty = bool.Parse(data.Value2);
                    return;

                case PropertyType.IsBandit:
                    instance.IsBandit = bool.Parse(data.Value2);
                    return;

                default: 
                    Logger.Error("{propertyType} is not supported", propertyType);
                    return;
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeMobilePartyProperty>(Handle);
        }
    }
}
