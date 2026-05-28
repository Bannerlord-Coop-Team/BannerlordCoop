using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.ItemObjects.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public string WeaponId;

    [ProtoMember(2)]
    public string StringName;

    public NetworkSetCraftedWeaponNameServer(
        string weaponId,
        string stringName)
    {
        WeaponId = weaponId;
        StringName = stringName;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public string WeaponId;

    [ProtoMember(2)]
    public string StringName;

    public NetworkSetCraftedWeaponNameClients(NetworkSetCraftedWeaponNameServer cloneObject)
    {
        WeaponId = cloneObject.WeaponId;
        StringName = cloneObject.StringName;
    }
}