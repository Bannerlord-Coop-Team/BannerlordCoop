using Missions.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Agents;

// #4492: the per-frame equipment compare must keep the old value semantics without the reflective struct equality.
public class AgentEquipmentEqualityTests
{
    private static AgentEquipmentData Loadout(params AgentArrowAmmoData[] ammo) =>
        new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0, "bow", "", ammo);

    [Fact]
    public void NullAndEmptyArrowAmmo_AreTheSameLoadout()
    {
        Assert.True(Loadout(null).Equals(Loadout(Array.Empty<AgentArrowAmmoData>())));
        Assert.True(Loadout(Array.Empty<AgentArrowAmmoData>()).Equals(Loadout(null)));
    }

    [Fact]
    public void ArrowAmmoPresentVersusMissing_IsDifferent()
    {
        var stack = new AgentArrowAmmoData(1, "arrows", null, 30);

        Assert.False(Loadout(stack).Equals(Loadout(null)));
        Assert.False(Loadout(null).Equals(Loadout(stack)));
    }

    [Fact]
    public void ItemModifierPresentVersusAbsent_IsDifferent()
    {
        var plain = new AgentArrowAmmoData(1, "arrows", null, 30);
        var sharp = new AgentArrowAmmoData(1, "arrows", "sharp", 30);

        Assert.False(plain.Equals(sharp));
        Assert.False(Loadout(plain).Equals(Loadout(sharp)));
    }

    [Theory]
    [InlineData(2, "arrows", null, 30, false)]
    [InlineData(1, "bolts", null, 30, false)]
    [InlineData(1, "arrows", "sharp", 30, false)]
    [InlineData(1, "arrows", null, 29, false)]
    [InlineData(1, "arrows", null, 30, true)]
    public void ArrowAmmoData_ComparesEveryField(int slot, string itemId, string modifierId, short amount, bool equal)
    {
        var baseline = new AgentArrowAmmoData(1, "arrows", null, 30);
        var other = new AgentArrowAmmoData(slot, itemId, modifierId, amount);

        Assert.Equal(equal, baseline.Equals(other));
        Assert.Equal(equal, baseline.Equals((object)other));
        if (equal) Assert.Equal(baseline.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    public void ArrowAmmoItemIds_AreComparedByValueNotReference()
    {
        var left = new AgentArrowAmmoData(1, new string("arrows".ToCharArray()), new string("sharp".ToCharArray()), 30);
        var right = new AgentArrowAmmoData(1, new string("arrows".ToCharArray()), new string("sharp".ToCharArray()), 30);

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ArrowAmmoOrderOrCount_IsDifferent()
    {
        var first = new AgentArrowAmmoData(1, "arrows", null, 30);
        var second = new AgentArrowAmmoData(2, "bolts", null, 10);

        Assert.True(Loadout(first, second).Equals(Loadout(first, second)));
        Assert.False(Loadout(first, second).Equals(Loadout(second, first)));
        Assert.False(Loadout(first, second).Equals(Loadout(first)));
    }

    [Fact]
    public void AmountChange_IsDifferent()
    {
        Assert.False(Loadout(new AgentArrowAmmoData(1, "arrows", null, 30))
            .Equals(Loadout(new AgentArrowAmmoData(1, "arrows", null, 29))));
    }
}
