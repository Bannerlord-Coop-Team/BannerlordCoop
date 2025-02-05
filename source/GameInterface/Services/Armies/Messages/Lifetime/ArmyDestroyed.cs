using Common.Messaging;
using GameInterface.Services.Armies.Data;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Command to destroy a army.
/// </summary>
public record ArmyDestroyed : ICommand
{
    public ArmyDestructionData Data { get; }
    public Army Army { get; }

    public ArmyDestroyed(ArmyDestructionData data, Army army)
    {
        Data = data;
        Army = army;
    }
}
