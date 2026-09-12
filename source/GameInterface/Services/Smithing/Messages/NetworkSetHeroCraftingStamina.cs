using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSetHeroCraftingStamina : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    [ProtoMember(2)]
    public readonly int Value;

    public NetworkSetHeroCraftingStamina(string craftingHeroId, int value)
    {
        CraftingHeroId = craftingHeroId;
        Value = value;
    }
}