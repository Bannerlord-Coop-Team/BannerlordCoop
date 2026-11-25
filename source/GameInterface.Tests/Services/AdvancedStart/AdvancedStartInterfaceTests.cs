using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart;
using GameInterface.Services.AdvancedStart.Data;
using GameInterface.Services.AdvancedStart.Interfaces;
using SandBox.AdvancedStartOptions;
using System;
using System.Collections.Generic;
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
        var modifiers = new[] { new FakeModifier("PersonalShip", limit: 1), new FakeModifier("SwitchedOff", limit: 0) };

        var policy = AdvancedStartInterface.CreatePolicy(Array.Empty<IAdvancedStartType>(), modifiers, null,
            false, Array.Empty<string>(), new[] { "vlandia" });

        Assert.Equal(new Dictionary<string, int> { ["PersonalShip"] = 1 }, policy.ModifierLimits);
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
            new[] { new FakeModifier("PersonalShip", limit: 1) }, null, false, Array.Empty<string>(), new[] { "vlandia" });

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
            modifierLimits: null,
            defaultStartType: "default",
            worldStarted: worldStarted,
            playerRuledKingdomIds: null,
            joinableKingdomIds: null);

        Assert.Equal(expected, AdvancedStartInterface.OffersPickableStart(policy));
    }

    [Theory]
    // The default start with a ticked modifier gets only the modifier.
    [InlineData("default", "True", true, true, false, true, true)]
    // A start the world can no longer run falls back to the default start and still gets the ticked modifier.
    [InlineData("outlaw", "True", true, false, false, true, false)]
    // A ticked modifier the host doesn't offer is skipped.
    [InlineData("outlaw", "True", false, true, true, false, true)]
    // The client sends an unticked modifier as "False".
    [InlineData("outlaw", "False", true, true, true, false, true)]
    public void PickStart_RunsTickedOfferedModifiersWhateverTheStart(string startTypeId, string shipValue, bool shipOffered,
        bool startCanRun, bool expectStart, bool expectShip, bool expectApplies)
    {
        var outlaw = new FakeStartType("outlaw", startCanRun);
        var ship = new FakeModifier("PersonalShip", limit: shipOffered ? 1 : 0);
        var policy = AdvancedStartInterface.CreatePolicy(new[] { outlaw }, new[] { ship }, null,
            false, Array.Empty<string>(), new[] { "vlandia" });
        var selections = new[]
        {
            new AdvancedStartSelection("StartType", startTypeId),
            new AdvancedStartSelection("KingdomId", "vlandia"),
            new AdvancedStartSelection("PersonalShip", shipValue),
        };

        var pick = AdvancedStartInterface.PickStart(policy, new[] { outlaw }, new[] { ship }, selections);

        Assert.Equal(expectStart ? outlaw : null, pick.StartType);
        Assert.Equal(expectShip ? new[] { ((IAdvancedStartModifier)ship, 1) } : Array.Empty<(IAdvancedStartModifier, int)>(), pick.Modifiers);
        Assert.Equal(expectApplies, pick.StartApplies);
    }

    [Theory]
    [InlineData("2", 2)]
    // A number above the host's limit is capped at the limit.
    [InlineData("5", 3)]
    [InlineData("0", 0)]
    public void PickStart_CapsANumberAtTheHostsLimit(string pickedValue, int expected)
    {
        var siblings = new FakeModifier("CoopSiblings", limit: 3);
        var policy = AdvancedStartInterface.CreatePolicy(Array.Empty<IAdvancedStartType>(), new[] { siblings }, null,
            false, Array.Empty<string>(), new[] { "vlandia" });

        var pick = AdvancedStartInterface.PickStart(policy, Array.Empty<IAdvancedStartType>(), new[] { siblings },
            new[] { new AdvancedStartSelection("CoopSiblings", pickedValue) });

        Assert.Equal(expected == 0 ? Array.Empty<(IAdvancedStartModifier, int)>() : new[] { ((IAdvancedStartModifier)siblings, expected) },
            pick.Modifiers);
    }

    [Theory]
    [InlineData("king", false, "king")]
    // Config keys spell ids in camel case, so the host's default matches ignoring case.
    [InlineData("KING", false, "king")]
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

    private sealed class FakeStartType : IAdvancedStartType
    {
        private readonly bool canRunInWorld;

        public FakeStartType(string id, bool canRunInWorld)
        {
            Id = id;
            this.canRunInWorld = canRunInWorld;
        }

        public string Id { get; }
        public bool ChangesWorld => false;
        public bool NeedsKingdom => false;
        public bool IsOffered(AdvancedStartConfigData config) => true;
        public bool CanRunInWorld() => canRunInWorld;
        public void Run(Hero hero, string kingdomId) => throw new NotSupportedException();
    }

    private sealed class FakeModifier : IAdvancedStartModifier
    {
        private readonly int limit;

        public FakeModifier(string optionId, int limit)
        {
            OptionId = optionId;
            this.limit = limit;
        }

        public string OptionId { get; }
        public int GetLimit(AdvancedStartConfigData config) => limit;
        public AdvancedStartOption CreateOption(int limit) => null;
        public void Run(Hero hero, int value) => throw new NotSupportedException();
    }
}
