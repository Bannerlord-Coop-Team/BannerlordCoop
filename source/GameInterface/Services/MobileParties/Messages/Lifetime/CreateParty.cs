using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Command to create a new party.
/// </summary>
public record CreateParty : ICommand
{
    public PartyCreationData Data { get; }

    public CreateParty(PartyCreationData data)
    {
        Data = data;
    }
}
