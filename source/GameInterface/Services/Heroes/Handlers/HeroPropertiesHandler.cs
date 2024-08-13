using System.Collections.Generic;
using Common.Messaging;
using GameInterface.Common.Handlers;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

public class HeroPropertiesHandler : AbstractCommandHandler<HeroPropertiesHandler, Hero>
{
    public HeroPropertiesHandler(IMessageBroker messageBroker, IObjectManager objectManager) : base(messageBroker, objectManager)
    {
    }

    public override HashSet<string> GetPropertyOrFieldNames()
    {
        return new HashSet<string>
        {
            "StaticBodyProperties",
            "Weight",
            "Build",
            "PassedTimeAtHomeSettlement",
            "EncyclopediaText",
            "IsFemale",
            "_battleEquipment",
            "_civilianEquipment",
            "CaptivityStartTime",
            "PreferredUpgradeFormation",
            "HeroState",
            "IsMinorFactionHero",
            "Issue",
            "CompanionOf",
            "Occupation",
            "DeathMark",
            "DeathMarkKillerHero",
            "LastKnownClosestSettlement",
            "HitPoints",
            "DeathDay",
            "LastExaminedLogEntryID",
            "Clan",
            "SupporterOf",
            "GovernorOf",
            "OwnedAlleys",
            "OwnedCaravans",
            "PartyBelongedTo",
            "PartyBelongedToAsPrisoner",
            "StayingInSettlement",
            "IsKnownToPlayer",
            "HasMet",
            "LastMeetingTimeWithPlayer",
            "BornSettlement",
            "Gold",
            "RandomValue",
            "BannerItem",
            "Father",
            "Mother",
            "Spouse"
        };
    }
}