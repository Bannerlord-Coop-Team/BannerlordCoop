using Common.Messaging;

namespace GameInterface.Services.Clans.Messages;

/// <summary>
/// Local event when a clan name is changed from game interface
/// </summary>
public record ClanNameChanged : IEvent
{
    public string ClanId { get; }
    public string Name { get; }
    public string InformalName { get; }

    public ClanNameChanged(string clanId, string name, string informalName)
    {
        ClanId = clanId;
        Name = name;
        InformalName = informalName;
    }
}
