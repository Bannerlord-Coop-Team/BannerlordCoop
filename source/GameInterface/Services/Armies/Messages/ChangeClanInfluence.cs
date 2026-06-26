using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a clan influence is changed
/// </summary>
public readonly struct ChangeClanInfluence : IEvent
{
    public readonly Clan PlayerClan;
    public readonly int Influence;

    public ChangeClanInfluence(Clan playerClan, int influence)
    {
        PlayerClan = playerClan;
        Influence = influence;
    }
}
