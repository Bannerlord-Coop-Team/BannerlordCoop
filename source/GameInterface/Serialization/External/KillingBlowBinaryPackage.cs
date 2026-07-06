using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External;

/// <summary>Binary package for serializing vanilla <see cref="KillingBlow"/> fields.</summary>
[Serializable]
public class KillingBlowBinaryPackage : BinaryPackageBase<KillingBlow>
{
    public KillingBlowBinaryPackage(KillingBlow obj, IBinaryPackageFactory binaryPackageFactory)
        : base(obj, binaryPackageFactory)
    {
    }

    protected override void PackInternal()
    {
        base.PackFields();
    }

    protected override void UnpackInternal()
    {
        base.UnpackFields();
    }
}
