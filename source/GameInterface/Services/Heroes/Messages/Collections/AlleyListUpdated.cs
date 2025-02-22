using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record AlleyListUpdated : GenericListEvent<Hero, Alley>
{
    public AlleyListUpdated(Hero instance, Alley value) : base(instance, value)
    {
        Instance = instance;
        Value = value;
    }
    public Hero Instance { get; }
    public Alley Value { get; }
}
