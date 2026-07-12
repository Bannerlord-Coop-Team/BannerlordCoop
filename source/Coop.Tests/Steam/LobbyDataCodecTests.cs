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
            var info = new SessionJoinInfo
            {
                Address = "203.0.113.7",
                Port = 4200,
                ServerSteamId = 76561198000000042,
                ModVersion = "1.2.3+abc123",
                PasswordRequired = true,
                Password = "must-not-be-advertised",
            };

            var encoded = LobbyDataCodec.Encode(info);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out var error));

            Assert.Null(error);
            Assert.Equal(SessionJoinInfo.CurrentVersion, decoded.Version);
            Assert.Equal("203.0.113.7", decoded.Address);
            Assert.Equal(4200, decoded.Port);
            Assert.Equal(76561198000000042UL, decoded.ServerSteamId);
            Assert.Equal("1.2.3+abc123", decoded.ModVersion);
            Assert.True(decoded.PasswordRequired);
            Assert.Null(decoded.Password);
            Assert.True(decoded.HasAddress);
            Assert.True(decoded.HasServerSteamId);
            Assert.DoesNotContain(encoded.Values, value => value.Contains("must-not-be-advertised"));
            Assert.Equal(LobbyDataCodec.StandaloneLobbyType, encoded[LobbyDataCodec.LobbyTypeKey]);
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
            Assert.False(decoded.HasServerSteamId);
            Assert.True(string.IsNullOrEmpty(decoded.ModVersion));
            Assert.False(decoded.PasswordRequired);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        [InlineData("True")]
        public void Decode_AcceptsPasswordRequiredFlag(string value)
        {
            var data = LobbyDataCodec.Encode(new SessionJoinInfo { Port = 4200 });
            var mutable = new Dictionary<string, string>(data)
            {
                [LobbyDataCodec.PasswordRequiredKey] = value,
            };

            Assert.True(LobbyDataCodec.TryDecode(key => Read(mutable, key), out var decoded, out _));
            Assert.True(decoded.PasswordRequired);
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
