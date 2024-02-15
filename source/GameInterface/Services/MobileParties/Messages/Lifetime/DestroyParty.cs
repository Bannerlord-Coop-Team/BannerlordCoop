using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Command to destroy a party.
/// </summary>
public record DestroyParty : ICommand
{
    public PartyDestructionData Data { get; }

    public DestroyParty(PartyDestructionData data)
    {
        Data = data;
    }
}
