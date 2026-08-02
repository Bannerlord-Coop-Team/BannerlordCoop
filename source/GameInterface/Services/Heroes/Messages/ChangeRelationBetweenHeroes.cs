using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct ChangeRelationBetweenHeroes : IEvent
{
    public readonly Hero Hero1;
    public readonly Hero Hero2;
    public readonly int Relation;

    public ChangeRelationBetweenHeroes(Hero hero1, Hero hero2, int relation)
    {
        Hero1 = hero1;
        Hero2= hero2;
        Relation = relation;
    }
}
