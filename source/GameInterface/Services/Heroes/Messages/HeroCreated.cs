using Common.Messaging;
using Coop.Mod.Extentions;
using GameInterface.Services.Heroes.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages;

public record HeroCreated : IEvent
{
    public HeroCreationData HeroCreatedData { get; }

    public HeroCreated(
       CharacterObject template,
        int age,
        CampaignTime birthday,
        Settlement bornSettlement)
    {
        HeroCreatedData = new HeroCreationData(
            template.StringId,
            age,
            birthday.GetNumTicks(),
            bornSettlement.StringId);
    }
}