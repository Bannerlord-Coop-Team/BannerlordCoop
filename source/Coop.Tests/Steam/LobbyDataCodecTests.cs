using Common.Network.Session;
using Coop.Steam;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Steam
{
    public class LobbyDataCodecTests
    {
        private static string Read(IReadOnlyDictionary<string, string> data, string key)
        {
            return data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        [Fact]
        public void RoundTrip_PreservesJoinInfo()
        {
            var info = new SessionJoinInfo { Address = "203.0.113.7", Port = 4200 };

            var encoded = LobbyDataCodec.Encode(info);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out var error));

            Assert.Null(error);
            Assert.Equal(SessionJoinInfo.CurrentVersion, decoded.Version);
            Assert.Equal("203.0.113.7", decoded.Address);
            Assert.Equal(4200, decoded.Port);
            Assert.True(decoded.HasAddress);
        }

        [Fact]
        public void RoundTrip_PreservesEmptyAddress()
        {
            var info = new SessionJoinInfo { Address = null, Port = 4200 };

            var encoded = LobbyDataCodec.Encode(info);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out _));

            Assert.False(decoded.HasAddress);
        }

        [Fact]
        public void Decode_FailsWithoutVersion()
        {
            Assert.False(LobbyDataCodec.TryDecode(_ => string.Empty, out var info, out var error));

            Assert.Null(info);
            Assert.NotNull(error);
        }

        [Fact]
        public void Decode_AcceptsOlderVersion()
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = "1",
                [LobbyDataCodec.AddressKey] = "203.0.113.7",
                [LobbyDataCodec.PortKey] = "4200",
            };

            Assert.True(LobbyDataCodec.TryDecode(key => Read(data, key), out var decoded, out _));

            Assert.Equal(1, decoded.Version);
            Assert.True(decoded.Version < SessionJoinInfo.MinTunnelVersion);
        }

        [Fact]
        public void Decode_FailsOnNewerVersion()
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = (SessionJoinInfo.CurrentVersion + 1).ToString(),
                [LobbyDataCodec.AddressKey] = "203.0.113.7",
                [LobbyDataCodec.PortKey] = "4200",
            };

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("newer", error);
        }

        [Theory]
        [InlineData("not-a-port")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("70000")]
        public void Decode_FailsOnInvalidPort(string port)
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = SessionJoinInfo.CurrentVersion.ToString(),
                [LobbyDataCodec.PortKey] = port,
            };

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.NotNull(error);
        }
    }
}
