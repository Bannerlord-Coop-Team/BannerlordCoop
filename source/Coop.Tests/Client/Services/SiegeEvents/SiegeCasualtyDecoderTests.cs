using Common.Util;
using Coop.Core.Client.Services.SiegeEvents;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Client.Services.SiegeEvents;

public class SiegeCasualtyDecoderTests
{
    private readonly Mock<IObjectManager> objects = new();
    private readonly CharacterObject troop = ObjectHelper.SkipConstructor<CharacterObject>();
    private readonly SiegeCasualtyDecoder decoder;

    public SiegeCasualtyDecoderTests()
    {
        var character = troop;
        objects.Setup(manager => manager.TryGetObjectWithLogging(5u, out character)).Returns(true);
        decoder = new SiegeCasualtyDecoder(objects.Object);
    }

    [Fact]
    public void IdsAndCounts_DecodeIntoTheCasualtyRoster()
    {
        Assert.True(decoder.TryDecode(new[] { 5u }, new[] { 7 }, out var casualties));
        Assert.Equal(7, casualties.GetTroopRoster()[0].Number);
        Assert.Same(troop, casualties.GetTroopRoster()[0].Character);
    }

    [Fact]
    public void NullArrays_ReadAsNoCasualties()
    {
        Assert.True(decoder.TryDecode(null, null, out var casualties));
        Assert.Equal(0, casualties.TotalManCount);
    }

    [Fact]
    public void MismatchedLengths_AreRefused()
    {
        Assert.False(decoder.TryDecode(new[] { 5u }, null, out _));
    }

    [Fact]
    public void UnknownTroopId_IsRefused()
    {
        Assert.False(decoder.TryDecode(new[] { 9u }, new[] { 1 }, out _));
    }
}
