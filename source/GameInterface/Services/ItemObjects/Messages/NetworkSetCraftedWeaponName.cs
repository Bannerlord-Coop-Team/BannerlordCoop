using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.ItemObjects.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public readonly string WeaponId;

    [ProtoMember(2)]
    public readonly string StringName;

    public NetworkSetCraftedWeaponNameServer(
        string weaponId,
        string stringName)
    {
        WeaponId = weaponId;
        StringName = stringName;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public readonly string WeaponId;

    [ProtoMember(2)]
    public readonly string StringName;

    public NetworkSetCraftedWeaponNameClients(NetworkSetCraftedWeaponNameServer cloneObject)
    {
        WeaponId = cloneObject.WeaponId;
        StringName = cloneObject.StringName;
    }
}