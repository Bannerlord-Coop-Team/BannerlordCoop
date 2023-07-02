using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages;

public record RemoveMainParty : ICommand
{
}

public record MainPartyRemoved : IResponse
{
}
