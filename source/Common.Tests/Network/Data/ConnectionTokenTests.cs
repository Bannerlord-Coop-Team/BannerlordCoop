using Common.Network.Data;
using System;
using Xunit;

namespace Common.Tests.Network.Data;

public class ConnectionTokenTests
{
    [Fact]
    public void TryParse_RejectsAnOversizedToken()
    {
        Assert.False(ConnectionToken.TryParse(
            new string('x', ConnectionToken.MaxSerializedLength + 1), out _));
    }

    [Fact]
    public void Constructor_RejectsAValueThatCannotBeReadFromConnectionData()
    {
        Assert.Throws<ArgumentException>(() => new ConnectionToken(
            new string('x', ConnectionToken.MaxSerializedLength), "instance"));
    }

    [Fact]
    public void Credential_RoundTripsThroughTheSerializedToken()
    {
        var credential = Guid.NewGuid();
        var serialized = (string)new ConnectionToken("controller", "instance", credential);

        Assert.True(ConnectionToken.TryParse(serialized, out var parsed));
        Assert.Equal("controller", parsed.ControllerId);
        Assert.Equal("instance", parsed.InstanceId);
        Assert.Equal(credential, parsed.PeerCredential);
    }

    [Fact]
    public void TryParse_RejectsAnInvalidCredential()
    {
        Assert.False(ConnectionToken.TryParse("controller%instance%not-a-guid", out _));
    }
}
