using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart;
using GameInterface.Services.AdvancedStart.Data;
using GameInterface.Services.AdvancedStart.Interfaces;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.AdvancedStart;

public class AdvancedStartInterfaceTests
{
    [Fact]
    public void CreatePolicy_OffersDefaultFirst_AndListsEachStartTypeTrait()
    {
        var policy = AdvancedStartInterface.CreatePolicy(VanillaAdvancedStartType.All, Array.Empty<IAdvancedStartModifier>(), null,
            false, Array.Empty<string>(), new[] { "vlandia" });

        Assert.Equal(new[] { "default", "outlaw", "beggar", "trader", "mercenary", "vassal", "king" }, policy.StartTypes);
        Assert.Equal(new[] { "trader", "mercenary", "vassal", "king" }, policy.WorldStartTypes);
        Assert.Equal(new[] { "mercenary", "vassal", "king" }, policy.KingdomStartTypes);
    }

    [Fact]
    public void CreatePolicy_ListsOnlyOfferedModifiers()
    {
        var modifiers = new[] { new FakeModifier("PersonalShip", offered: true), new FakeModifier("SwitchedOff", offered: false) };

        var policy = AdvancedStartInterface.CreatePolicy(Array.Empty<IAdvancedStartType>(), modifiers, null,
            false, Array.Empty<string>(), new[] { "vlandia" });

        Assert.Equal(new[] { "PersonalShip" }, policy.ModifierIds);
    }

    [Fact]
    public void OffersPickableStart_NullPolicy_IsFalse()
    {
        Assert.False(AdvancedStartInterface.OffersPickableStart(null));
    }

    [Fact]
    public void OffersPickableStart_OnlyAModifier_IsTrue()
    {
        // The default start with a ship on top is still a pick worth opening the window for.
        var policy = AdvancedStartInterface.CreatePolicy(Array.Empty<IAdvancedStartType>(),
            new[] { new FakeModifier("PersonalShip", offered: true) }, null, false, Array.Empty<string>(), new[] { "vlandia" });

        Assert.True(AdvancedStartInterface.OffersPickableStart(policy));
    }

    [Theory]
    // Outlaw doesn't change the world, so it stays pickable after the clock moves.
    [InlineData(new[] { "default", "outlaw" }, true, new string[0], new[] { "vlandia" }, true)]
    // Only world starts are offered and the clock has moved.
    [InlineData(new[] { "default", "trader", "mercenary", "vassal", "king" }, true, new string[0], new[] { "vlandia" }, false)]
    // A king start with every joinable kingdom already ruled by a player.
    [InlineData(new[] { "default", "king" }, false, new[] { "vlandia" }, new[] { "vlandia" }, false)]
    // A vassal start can still join a kingdom a player rules.
    [InlineData(new[] { "default", "vassal" }, false, new[] { "vlandia" }, new[] { "vlandia" }, true)]
    // A kingdom start in a world with no joinable kingdoms.
    [InlineData(new[] { "default", "mercenary" }, false, new string[0], new string[0], false)]
    public void OffersPickableStart_FollowsWorldAndKingdomLocks(
        string[] startTypes, bool worldStarted, string[] playerRuled, string[] joinable, bool expected)
    {
        var policy = CreateVanillaPolicy(startTypes, "default", worldStarted, playerRuled, joinable);

        Assert.Equal(expected, AdvancedStartInterface.OffersPickableStart(policy));
    }

    [Theory]
    // A king start with no joinable kingdom.
    [InlineData(new[] { "default", "king" }, new[] { "king" }, false, false)]
    // A host offering only outlaw sends every start type trait array empty.
    [InlineData(new[] { "default", "outlaw" }, null, true, true)]
    public void OffersPickableStart_NullArraysFromTheWire_CountAsEmpty(
        string[] startTypes, string[] traitStartTypes, bool worldStarted, bool expected)
    {
        // Empty arrays arrive as null over protobuf.
        var policy = new AdvancedStartPolicy(startTypes,
            worldStartTypes: traitStartTypes,
            kingdomStartTypes: traitStartTypes,
            modifierIds: null,
            defaultStartType: "default",
            worldStarted: worldStarted,
            playerRuledKingdomIds: null,
            joinableKingdomIds: null);

        Assert.Equal(expected, AdvancedStartInterface.OffersPickableStart(policy));
    }

    [Theory]
    [InlineData("king", false, "king")]
    // The host's default is a world start and the clock has moved.
    [InlineData("king", true, "default")]
    // The host's default isn't offered.
    [InlineData("beggar", false, "default")]
    public void GetDefaultStartType_FallsBackWhenHostDefaultCantBePicked(string hostDefault, bool worldStarted, string expected)
    {
        var policy = CreateVanillaPolicy(new[] { "default", "outlaw", "king" }, hostDefault, worldStarted,
            Array.Empty<string>(), new[] { "vlandia" });

        Assert.Equal(expected, AdvancedStartInterface.GetDefaultStartType(policy));
    }

    // The policy the server builds when it offers these vanilla start types.
    private static AdvancedStartPolicy CreateVanillaPolicy(string[] startTypes, string defaultStartType, bool worldStarted,
        string[] playerRuled, string[] joinable) =>
        AdvancedStartInterface.CreatePolicy(VanillaAdvancedStartType.All.Where(startType => startTypes.Contains(startType.Id)),
            Array.Empty<IAdvancedStartModifier>(), new AdvancedStartConfigData { DefaultStartType = defaultStartType },
            worldStarted, playerRuled, joinable);

    private sealed class FakeModifier : IAdvancedStartModifier
    {
        private readonly bool offered;

        public FakeModifier(string optionId, bool offered)
        {
            OptionId = optionId;
            this.offered = offered;
        }

        public string OptionId { get; }
        public bool IsOffered(AdvancedStartConfigData config) => offered;
        public void Run(Hero hero) => throw new NotSupportedException();
    }
}
