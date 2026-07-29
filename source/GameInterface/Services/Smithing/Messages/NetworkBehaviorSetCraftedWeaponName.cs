using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBehaviorSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftedWeaponId;

    [ProtoMember(2)]
    public readonly TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameServer(
        string craftedWeaponId,
        TextObject name)
    {
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBehaviorSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftedWeaponId;

    [ProtoMember(2)]
    public readonly TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameClients(NetworkBehaviorSetCraftedWeaponNameServer cloneObject)
    {
        CraftedWeaponId = cloneObject.CraftedWeaponId;
        Name = cloneObject.Name;
    }
}