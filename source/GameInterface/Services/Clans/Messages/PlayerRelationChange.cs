using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct PlayerRelationChange : IEvent
{
    public readonly Hero Hero;
    public readonly int Relation;

    public PlayerRelationChange(Hero hero, int relation)
    {
        Hero = hero;
        Relation = relation;
    }
}
