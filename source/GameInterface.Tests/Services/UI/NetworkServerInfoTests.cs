using Common.Serialization;
using GameInterface.Services.UI.ServerInfo;
using ProtoBuf;
using System;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the one message that carries the whole server info to a joining player.</summary>
public class NetworkServerInfoTests
{
    // Every section travels in one message and keeps every character.
    [Fact]
    public void AllSectionsRoundTripInOneMessage()
    {
        var info = new NetworkServerInfo(
            new[] { "Willkommen überall, 欢迎 \U0001F600", "{PLAYER} <b>bold</b>", new string('a', ServerInfoLimits.MaxMotdLength) },
            new[] { "Be kind", "{=coop_server_info_title}x" },
            new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" }, new ServerInfoLink { Label = "", Url = "https://example.com/" } },
            new[] { new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." } });

        var copy = Serializer.DeepClone(info);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var wire = Assert.IsType<NetworkServerInfo>(serializer.Deserialize(serializer.Serialize(info)));

        foreach (var received in new[] { copy, wire })
        {
            Assert.Equal(info.Motd, received.Motd);
            Assert.Equal(info.Rules, received.Rules);
            Assert.Equal(info.Links, received.Links);
            Assert.Equal(info.News, received.News);
        }
    }

    // The client treats a missing section as empty; this pins down that protobuf sends an empty one as null.
    [Fact]
    public void EmptySectionsArriveAsNull()
    {
        var info = new NetworkServerInfo(new[] { "Welcome" }, Array.Empty<string>(), Array.Empty<ServerInfoLink>(), null);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        var wire = Assert.IsType<NetworkServerInfo>(serializer.Deserialize(serializer.Serialize(info)));

        Assert.Equal(new[] { "Welcome" }, wire.Motd);
        Assert.Null(wire.Rules);
        Assert.Null(wire.Links);
        Assert.Null(wire.News);
    }
}
