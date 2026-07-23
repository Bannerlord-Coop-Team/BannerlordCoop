using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkBehaviorSetCraftedWeaponNameServer : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftedWeaponId;

    [ProtoMember(3)]
    public string StringName;

    public NetworkBehaviorSetCraftedWeaponNameServer(
        string craftingCampaignBehaviorId,
        string craftedWeaponId,
        string stringName)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftedWeaponId = craftedWeaponId;
        StringName = stringName;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkBehaviorSetCraftedWeaponNameClients : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftedWeaponId;

    [ProtoMember(3)]
    public string StringName;

    public NetworkBehaviorSetCraftedWeaponNameClients(NetworkBehaviorSetCraftedWeaponNameServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
        CraftedWeaponId = cloneObject.CraftedWeaponId;
        StringName = cloneObject.StringName;
    }
}