using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;
public class DestroyParty : ICommand
{
    public PartyDestructionData Data { get; }

    public DestroyParty(PartyDestructionData data)
    {
        Data = data;
    }
}
