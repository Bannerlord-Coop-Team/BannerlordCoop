using Missions.Agents.Packets;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class AgentEquipmentDataTests
{
    [Fact]
    public void IncompleteMissionEquipment_IsNotSafeForNativeWieldChange()
    {
        var equipment = new MissionEquipment();
        SetWeaponSlots(equipment, (int)EquipmentIndex.NumAllWeaponSlots - 1);

        Assert.False(HasSafeWeaponSlots(equipment));
    }

    [Fact]
    public void CompleteMissionEquipment_IsSafeForNativeWieldChange()
    {
        var equipment = new MissionEquipment();
        SetWeaponSlots(equipment, (int)EquipmentIndex.NumAllWeaponSlots);

        Assert.True(HasSafeWeaponSlots(equipment));
    }

    [Fact]
    public void InvalidWeaponUsageIndex_IsNotSafeForNativeWieldChange()
    {
        var equipment = new MissionEquipment();
        SetWeaponSlots(equipment, (int)EquipmentIndex.NumAllWeaponSlots);
        equipment[EquipmentIndex.Weapon0] = CreateWeapon(weaponCount: 1, currentUsageIndex: 1);

        Assert.False(HasSafeWeaponSlots(equipment));
    }

    [Fact]
    public void IncomingWeaponUsageIndex_IsClampedToDestinationWeapon()
    {
        var equipment = new MissionEquipment();
        SetWeaponSlots(equipment, (int)EquipmentIndex.NumAllWeaponSlots);
        equipment[EquipmentIndex.Weapon0] = CreateWeapon(weaponCount: 2, currentUsageIndex: 0);

        Assert.Equal(1, GetSafeUsageIndex(equipment, EquipmentIndex.Weapon0, 1));
        Assert.Equal(0, GetSafeUsageIndex(equipment, EquipmentIndex.Weapon0, 2));
    }

    private static void SetWeaponSlots(MissionEquipment equipment, int count)
    {
        typeof(MissionEquipment)
            .GetField("_weaponSlots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(equipment, new MissionWeapon[count]);
    }

    private static bool HasSafeWeaponSlots(MissionEquipment equipment)
    {
        return (bool)typeof(AgentEquipmentData)
            .GetMethod("HasSafeWeaponSlots", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { equipment });
    }

    private static int GetSafeUsageIndex(
        MissionEquipment equipment,
        EquipmentIndex index,
        int usageIndex)
    {
        return (int)typeof(AgentEquipmentData)
            .GetMethod("GetSafeUsageIndex", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { equipment, index, usageIndex });
    }

    private static MissionWeapon CreateWeapon(int weaponCount, int currentUsageIndex)
    {
        var weapon = new MissionWeapon(new ItemObject(), null, null);
        object boxed = weapon;
        typeof(MissionWeapon)
            .GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(boxed, new List<WeaponComponentData>(new WeaponComponentData[weaponCount]));
        weapon = (MissionWeapon)boxed;
        weapon.CurrentUsageIndex = currentUsageIndex;
        return weapon;
    }
}
