using Missions.Agents.Packets;
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
}
