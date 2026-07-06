using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Surrogates;

/// <summary>protobuf-net surrogate for <see cref="KillingBlow"/>.</summary>
[ProtoContract(SkipConstructor = true)]
internal class KillingBlowSurrogate
{
    [ProtoMember(1)]
    public byte[] Data { get; }

    public KillingBlowSurrogate(KillingBlow obj)
    {
        if (obj.Equals(default(KillingBlow))) return;
        if (!ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory)) return;

        IBinaryPackage package = packageFactory.GetBinaryPackage(obj);
        Data = BinaryFormatterSerializer.Serialize(package);
    }

    private KillingBlow Deserialize()
    {
        if (Data == null) return default;
        if (!ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory)) return default;

        var package = BinaryFormatterSerializer.Deserialize<KillingBlowBinaryPackage>(Data);
        return package.Unpack<KillingBlow>(packageFactory);
    }

    public static implicit operator KillingBlowSurrogate(KillingBlow obj)
    {
        return new KillingBlowSurrogate(obj);
    }

    public static implicit operator KillingBlow(KillingBlowSurrogate surrogate)
    {
        return surrogate.Deserialize();
    }
}
