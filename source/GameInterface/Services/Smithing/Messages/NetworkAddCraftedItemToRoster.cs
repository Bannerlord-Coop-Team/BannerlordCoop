using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddCraftedItemToRoster : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftedItemId;

    [ProtoMember(2)]
    public readonly string PlayerHeroId;

    [ProtoMember(3)]
    public readonly bool IsFreeMode;

    [ProtoMember(4)]
    public readonly string WeaponModifierId;

    public NetworkAddCraftedItemToRoster(
        string craftedItemId,
        string playerHeroId,
        bool isFreeMode,
        string weaponModifierId)
    {
        CraftedItemId = craftedItemId;
        PlayerHeroId = playerHeroId;
        IsFreeMode = isFreeMode;
        WeaponModifierId = weaponModifierId;
    }
}
