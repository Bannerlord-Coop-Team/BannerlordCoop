using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Local event raised on the server when a hero relation changes, so it can be replicated to clients.
/// </summary>
public record HeroRelationChanged : IEvent
{
    public string Hero1Id { get; }
    public string Hero2Id { get; }
    public int Value { get; }

    public HeroRelationChanged(string hero1Id, string hero2Id, int value)
    {
        Hero1Id = hero1Id;
        Hero2Id = hero2Id;
        Value = value;
    }
}
