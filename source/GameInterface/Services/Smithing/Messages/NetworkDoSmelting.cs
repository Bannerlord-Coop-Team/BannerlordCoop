using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDoSmelting : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    [ProtoMember(2)]
    public readonly EquipmentElement EquipmentElement;

    public NetworkDoSmelting(
        string craftingHeroId,
        EquipmentElement equipmentElement)
    {
        CraftingHeroId = craftingHeroId;
        EquipmentElement = equipmentElement;
    }
}