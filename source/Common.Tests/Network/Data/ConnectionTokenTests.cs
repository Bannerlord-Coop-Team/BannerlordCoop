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
}
