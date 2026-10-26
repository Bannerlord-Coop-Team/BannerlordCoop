using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Modules;
using ProtoBuf;
using System.IO;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Connections.Messages;

public class NetworkModuleInfoTests
{
    [Fact]
    public void Serialize_WithName_RoundTripsName()
    {
        var module = new ModuleInfo("CalradiaExpanded", false, false, new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 0));
        var message = new NetworkModuleVersionsValidate(new[] { module }, null, id => id == "CalradiaExpanded" ? "Calradia Expanded" : null);

        var copy = RoundTrip(message);

        Assert.Equal("Calradia Expanded", Assert.Single(copy.Modules).Name);
    }

    [Fact]
    public void Serialize_WithoutName_LeavesNameNull()
    {
        // a client on an older coop build never sends field 5
        var module = new ModuleInfo("CalradiaExpanded", false, false, new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 0));
        var message = new NetworkModuleVersionsValidate(new[] { module }, null);

        var copy = RoundTrip(message);

        var copied = Assert.Single(copy.Modules);
        Assert.Equal("CalradiaExpanded", copied.Id);
        Assert.Null(copied.Name);
    }

    [Fact]
    public void Serialize_KingdomDecisionTypes_RoundTrip()
    {
        var message = new NetworkModuleVersionsValidate(System.Array.Empty<ModuleInfo>(), null) with
        {
            KingdomDecisionTypes = new[] { "SomeMod.CouncilSeatDecision" }
        };

        var copy = RoundTrip(message);

        Assert.Equal("SomeMod.CouncilSeatDecision", Assert.Single(copy.KingdomDecisionTypes!));
    }

    private static NetworkModuleVersionsValidate RoundTrip(NetworkModuleVersionsValidate message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<NetworkModuleVersionsValidate>(stream);
    }
}