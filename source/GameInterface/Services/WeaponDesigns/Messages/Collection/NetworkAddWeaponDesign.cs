using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Messages.Collection;

/// <summary>
/// Command to add a weapon design to <see cref="Crafting._history"/>
/// </summary>
///
[ProtoContract(SkipConstructor = true)]
public record NetworkAddWeaponDesign : ICommand
{
    public NetworkAddWeaponDesign(WeaponDesignData weaponDesignData)
    {
        CraftingId = weaponDesignData.CraftingId;
        WeaponDesignId = weaponDesignData.WeaponDesignId;
    }

    [ProtoMember(1)]
    public string CraftingId { get; }
    [ProtoMember(2)]
    public string WeaponDesignId { get; }
}