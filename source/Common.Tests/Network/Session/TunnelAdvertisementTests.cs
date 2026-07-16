using Common.Network;
using Common.Network.Session;
using Moq;

namespace Common.Tests.Network.Session;

public class TunnelAdvertisementTests
{
    private sealed class FakeTunnelHost : ISessionTunnelHost
    {
        public bool StartThrows;
        public bool Listening;
        public int? StartedPort;

        public bool IsListening => Listening;
        public int PeerCount => 0;

        public void Start(int serverPort)
        {
            if (StartThrows) throw new InvalidOperationException("no relay");

            StartedPort = serverPort;
            Listening = true;
        }

        public void Stop() => Listening = false;

        public bool TryGetRemoteSteamId(System.Net.IPEndPoint serverPeerEndpoint, out ulong steamId)
        {
            steamId = 0;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private static INetworkConfig Config(string address, bool tunneled = false)
    {
        var config = new Mock<INetworkConfig>();
        config.Setup(c => c.Address).Returns(address);
        config.Setup(c => c.Port).Returns(4200);
        config.Setup(c => c.IsTunneled).Returns(tunneled);
        return config.Object;
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void LoopbackSession_StartsTunnelAndKeepsTunnelVersion(string address)
    {
        var host = new FakeTunnelHost();
        var info = new SessionJoinInfo { Port = 4200 };

        TunnelAdvertisement.StartAndStamp(host, Config(address), info);

        Assert.Equal(4200, host.StartedPort);
        Assert.Equal(SessionJoinInfo.CurrentVersion, info.Version);
    }

    [Fact]
    public void RemoteSession_NeverStartsTunnelAndAdvertisesDirectOnly()
    {
        var host = new FakeTunnelHost();
        var info = new SessionJoinInfo { Address = "203.0.113.7", Port = 4200 };

        TunnelAdvertisement.StartAndStamp(host, Config("203.0.113.7"), info);

        Assert.Null(host.StartedPort);
        Assert.True(info.Version < SessionJoinInfo.MinTunnelVersion);
    }

    [Fact]
    public void TunneledJoiner_NeverStartsTunnelDespiteLoopbackAddress()
    {
        var host = new FakeTunnelHost();
        var info = new SessionJoinInfo { Port = 4200 };

        TunnelAdvertisement.StartAndStamp(host, Config("127.0.0.1", tunneled: true), info);

        Assert.Null(host.StartedPort);
        Assert.True(info.Version < SessionJoinInfo.MinTunnelVersion);
    }

    [Fact]
    public void FailedTunnelStart_IsCaughtAndAdvertisesDirectOnly()
    {
        var host = new FakeTunnelHost { StartThrows = true };
        var info = new SessionJoinInfo { Address = "203.0.113.7", Port = 4200 };

        TunnelAdvertisement.StartAndStamp(host, Config("localhost"), info);

        Assert.True(info.Version < SessionJoinInfo.MinTunnelVersion);
    }
}
