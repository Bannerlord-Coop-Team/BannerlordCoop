using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBehaviorSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public readonly string CraftedWeaponId;

    [ProtoMember(3)]
    public readonly TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameServer(
        string craftingCampaignBehaviorId,
        string craftedWeaponId,
        TextObject name)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBehaviorSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public readonly string CraftedWeaponId;

    [ProtoMember(3)]
    public readonly TextObject Name;

    public NetworkBehaviorSetCraftedWeaponNameClients(NetworkBehaviorSetCraftedWeaponNameServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
        CraftedWeaponId = cloneObject.CraftedWeaponId;
        Name = cloneObject.Name;
    }
}