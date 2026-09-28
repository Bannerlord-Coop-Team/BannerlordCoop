using GameInterface.AutoSync;
using GameInterface.Surrogates;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.AutoSync;

/// <summary>Pins <see cref="RawSerializer"/> output to the generic protobuf-net call it replaced.</summary>
public class RawSerializerTests
{
    // Value shapes AutoSync sends: primitives, enums, game types behind surrogates and plain contracts.
    private static readonly Dictionary<string, Func<object>> Values = new Dictionary<string, Func<object>>
    {
        ["int zero"] = () => 0,
        ["int"] = () => 1234567,
        ["int negative"] = () => -1,
        ["int min"] = () => int.MinValue,
        ["int max"] = () => int.MaxValue,
        ["long"] = () => 638_000_000_000L,
        ["uint max"] = () => uint.MaxValue,
        ["float"] = () => 0.37f,
        ["float negative zero"] = () => -0f,
        ["float nan"] = () => float.NaN,
        ["double"] = () => -1.5,
        ["bool false"] = () => false,
        ["bool true"] = () => true,
        ["string empty"] = () => string.Empty,
        ["string"] = () => "town_ES1",
        ["string unicode"] = () => "Danustica ş ü",
        ["enum"] = () => FormationClass.Cavalry,
        ["enum default"] = () => default(FormationClass),
        ["CampaignTime"] = () => new CampaignTime(123456789),
        ["CampaignTime zero"] = () => CampaignTime.Zero,
        ["Vec2"] = () => new Vec2(12.5f, -3.25f),
        ["Vec3"] = () => new Vec3(1024.25f, -2048.5f, 512.75f),
        ["CampaignVec2"] = () => new CampaignVec2(new Vec2(40f, 80.75f), false),
        ["ItemData"] = () => new ItemData(12.5f, 3.25f, 40, 1200),
        ["TextObject"] = () => new TextObject("{=12345678}Danustica"),
        ["TextObject with attributes"] = () => new TextObject("{=12345678}Testing with {INSERT}", new Dictionary<string, object>
        {
            ["INSERT"] = new TextObject("simple nests"),
            ["COUNT"] = 3,
            ["NAME"] = "Danustica",
        }),
        ["contract"] = () => new SyncedContract { Id = 7, Name = "town_comp_ES1" },
        ["derived contract"] = () => new DerivedSyncedContract { Id = 7, Extra = 2 },
    };

    public RawSerializerTests()
    {
        // Registers the game surrogates on RuntimeTypeModel.Default, as the mod does at startup.
        new SurrogateCollection();
    }

    public static IEnumerable<object[]> ValueNames() => Values.Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(ValueNames))]
    public void Serialize_WritesSameBytesAsGenericObjectCall(string name)
    {
        object value = Values[name]();

        Assert.Equal(LegacySerialize(value), RawSerializer.Serialize(value));
    }

    [Fact]
    public void Serialize_WritesNothingForNull()
    {
        Assert.Empty(LegacySerialize(null));
        Assert.Empty(RawSerializer.Serialize(null));
    }

    [Fact]
    public void Serialize_AllocatesLessThanGenericObjectCall()
    {
        object value = 1234567;
        LegacySerialize(value);
        RawSerializer.Serialize(value);

        long legacy = AllocatedBytes(() => LegacySerialize(value));
        long current = AllocatedBytes(() => RawSerializer.Serialize(value));

        // The generic object call allocates about 2 KB more per call.
        Assert.True(current * 2 < legacy, $"current {current} B, legacy {legacy} B");
    }

    private static long AllocatedBytes(Func<byte[]> serialize)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) serialize();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Frozen copy of RawSerializer.Serialize before the non-generic call, kept as the wire reference.
    private static byte[] LegacySerialize(object? obj)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            Serializer.Serialize(memoryStream, obj);
            return memoryStream.ToArray();
        }
    }

    /// <summary>Plain contract value with a subtype.</summary>
    [ProtoContract]
    [ProtoInclude(10, typeof(DerivedSyncedContract))]
    public class SyncedContract
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
    }

    /// <summary>Value whose runtime type differs from the declared member type.</summary>
    [ProtoContract]
    public class DerivedSyncedContract : SyncedContract
    {
        [ProtoMember(1)]
        public int Extra { get; set; }
    }
}
