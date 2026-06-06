using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the InRebelliousState changes in a town.
/// </summary>
public readonly struct TownInRebelliousStateChanged : ICommand
{
    public readonly Town Town;
    public readonly bool InRebelliousState;

    public TownInRebelliousStateChanged(Town town, bool inRebelliousState)
    {
        Town = town;
        InRebelliousState = inRebelliousState;
    }
}