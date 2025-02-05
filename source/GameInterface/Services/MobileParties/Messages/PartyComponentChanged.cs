using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;
public class PartyComponentChanged : IEvent
{
    public string PartyId { get; }
    public string ComponentId { get; }

    public PartyComponentChanged(string stringId, string componentId)
    {
        PartyId = stringId;
        ComponentId = componentId;
    }
}
