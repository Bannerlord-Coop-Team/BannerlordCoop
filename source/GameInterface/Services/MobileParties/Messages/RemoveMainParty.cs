using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

public record RemoveMainParty : ICommand
{
}

public record MainPartyRemoved : IEvent
{
}
