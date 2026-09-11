#if DEBUG
using System;
using System.Linq;
using Missions.Messages;
using ProtoBuf;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public sealed class NavalLabPresentationTests
{
    internal static NetworkNavalLabPresentation State(Guid ship, float phase = 0, float x = 0)
    {
        var frame = MatrixFrame.Identity;
        frame.origin.x = x;
        return new NetworkNavalLabPresentation(ship,
            new[] { new NetworkNavalLabSailPresentation("0:mast/0:sail", 0, 1, 1, 0, true, false, false, 0, 0) },
            new[] { new NetworkNavalLabOarPresentation("1:oar/0:seat", 0, phase, 1, 1, true, NetworkNavalLabOarPresentation.FromFrame(frame)) },
            new float[10]);
    }

    [Fact]
    public void OptionalPayloadRoundTripsBothHullIdentitiesAndBladeFrames()
    {
        var ships = new[] { State(Guid.NewGuid(), 3), State(Guid.NewGuid(), -3) };
        var message = new NetworkNavalLabFrames(Guid.NewGuid(), 1, 42, new float[24], 120,
            sailDeadlineUtcTicks: DateTime.UtcNow.AddSeconds(1).Ticks, presentation: ships);
        var copy = Serializer.DeepClone(message);
        Assert.Equal(message.Sequence, copy.Sequence);
        Assert.Equal(2, copy.Presentation.Length);
        for (int i = 0; i < 2; i++)
        {
            Assert.True(copy.Presentation[i].IsValid);
            Assert.Equal(ships[i].ShipId, copy.Presentation[i].ShipId);
            Assert.Equal(ships[i].Oars[0].BladeFrame, copy.Presentation[i].Oars[0].BladeFrame);
        }
        Assert.Null(Serializer.DeepClone(new NetworkNavalLabFrames(Guid.NewGuid(), 1, 1, new float[24])).Presentation);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(1001f)]
    public void MalformedScalarOrBladeBasisIsRejected(float value)
    {
        var state = State(Guid.NewGuid());
        state.Oars[0].BladeFrame[0] = value;
        Assert.False(state.IsValid);
        Assert.False(NetworkNavalLabOarPresentation.ValidFrame(new float[12]));
        Assert.False(NetworkNavalLabOarPresentation.ValidFrame(new float[11]));
    }

    [Fact]
    public void DuplicateStationAndOversizedInventoryAreRejected()
    {
        var state = State(Guid.NewGuid());
        Assert.False(new NetworkNavalLabPresentation(state.ShipId, state.Sails,
            new[] { state.Oars[0], state.Oars[0] }, state.Sides).IsValid);
        Assert.False(new NetworkNavalLabPresentation(state.ShipId,
            Enumerable.Repeat(state.Sails[0], 17).ToArray(), state.Oars, state.Sides).IsValid);
    }

    [Theory]
    [InlineData(3.1f, -3.1f)]
    [InlineData(-3.1f, 3.1f)]
    public void PhaseInterpolationCrossesWrapNotNeutral(float start, float end)
    {
        float phase = NetworkNavalLabPresentation.BlendPhase(start, end, 0.5f);
        Assert.True(Math.Abs(phase) > 3.1f);
        Assert.InRange(Math.Abs(phase), 3.1f, (float)Math.PI + 0.001f);
    }

    [Fact]
    public void BladeInterpolationPreservesOrthonormalBasis()
    {
        var start = State(Guid.NewGuid());
        var target = State(start.ShipId, 1, 2);
        var frame = NetworkNavalLabOarPresentation.ToFrame(target.Oars[0].BladeFrame);
        frame.rotation.RotateAboutUp(1);
        Array.Copy(NetworkNavalLabOarPresentation.FromFrame(frame), target.Oars[0].BladeFrame, 12);
        var halfway = target.BlendFrom(start, 0.5f);
        Assert.True(halfway.IsValid);
        Assert.Equal(1, halfway.Oars[0].BladeFrame[9], 4);
    }
}
#endif
