using GameInterface.Services.ObjectManager;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Data;

public interface IMissionWeaponDataMapper
{
    bool TryPack(MissionWeapon weapon, out MissionWeaponData data);
    bool TryResolve(MissionWeaponData data, out MissionWeapon weapon);
}

public class MissionWeaponDataMapper : IMissionWeaponDataMapper
{
    private readonly IObjectManager objectManager;

    public MissionWeaponDataMapper(IObjectManager objectManager)
    {
        if (objectManager == null) throw new System.ArgumentNullException(nameof(objectManager));

        this.objectManager = objectManager;
    }

    public bool TryPack(MissionWeapon weapon, out MissionWeaponData data)
    {
        data = null;

        string itemId = null;
        if (weapon.Item != null &&
            !objectManager.TryGetIdWithLogging(weapon.Item, out itemId))
        {
            return false;
        }

        string modifierId = null;
        if (weapon.ItemModifier != null &&
            !objectManager.TryGetIdWithLogging(weapon.ItemModifier, out modifierId))
        {
            return false;
        }

        data = new MissionWeaponData(
            itemId,
            modifierId,
            weapon.Banner,
            weapon.RawDataForNetwork,
            weapon.ReloadPhase,
            null);
        return true;
    }

    public bool TryResolve(MissionWeaponData data, out MissionWeapon weapon)
    {
        weapon = default;
        if (data == null) return false;

        ItemObject item = null;
        if (!string.IsNullOrEmpty(data.ItemObjectId) &&
            !objectManager.TryGetObjectWithLogging(data.ItemObjectId, out item))
        {
            return false;
        }

        ItemModifier modifier = null;
        if (!string.IsNullOrEmpty(data.ItemModifierId) &&
            !objectManager.TryGetObjectWithLogging(data.ItemModifierId, out modifier))
        {
            return false;
        }

        weapon = new MissionWeapon(
            item,
            modifier,
            data.Banner,
            data.DataValue,
            data.ReloadPhase,
            null);
        return true;
    }
}
