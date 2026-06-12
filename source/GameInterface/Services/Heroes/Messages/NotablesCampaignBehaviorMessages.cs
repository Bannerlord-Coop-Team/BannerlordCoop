using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Buildings.Messages;

public readonly struct UpdateNotableRelations : IEvent
{
    public readonly Hero Notable;

    public UpdateNotableRelations(Hero notable)
    {
        Notable = notable;
    }
}

public readonly struct UpdateNotableSupport : IEvent
{
    public readonly Hero Notable;

    public UpdateNotableSupport(Hero notable)
    {
        Notable = notable;
    }
}