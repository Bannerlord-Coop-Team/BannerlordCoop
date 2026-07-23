using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct WeaponComponentDataSurrogate
{
    [ProtoMember(1)]
    public WeaponComponentData.WeaponTiers WeaponTier { get; set; } = 0;
    [ProtoMember(2)]
    public string WeaponDescriptionId { get; set; } = "";
    [ProtoMember(3)]
    public int BodyArmor { get; set; } = 0;
    [ProtoMember(4)]
    public string PhysicsMaterial { get; set; } = "";
    [ProtoMember(5)]
    public string FlyingSoundCode { get; set; } = "";
    [ProtoMember(6)]
    public string PassbySoundCode { get; set; } = "";
    [ProtoMember(7)]
    public string ItemUsage { get; set; } = null;
    [ProtoMember(8)]
    public int ThrustSpeed { get; set; } = 0;
    [ProtoMember(9)]
    public int SwingSpeed { get; set; } = 0;
    [ProtoMember(10)]
    public int MissileSpeed { get; set; } = 0;
    [ProtoMember(11)]
    public int WeaponLength { get; set; } = 0;
    [ProtoMember(12)]
    public float WeaponBalance { get; set; } = 0;
    [ProtoMember(13)]
    public int ThrustDamage { get; set; } = 0;
    [ProtoMember(14)]
    public DamageTypes ThrustDamageType { get; set; } = DamageTypes.Invalid;
    [ProtoMember(15)]
    public int SwingDamage { get; set; } = 0;
    [ProtoMember(16)]
    public DamageTypes SwingDamageType { get; set; } = DamageTypes.Invalid;
    [ProtoMember(17)]
    public int FireDamage { get; set; } = 0;
    [ProtoMember(18)]
    public int Accuracy { get; set; } = 0;
    [ProtoMember(19)]
    public WeaponClass WeaponClass { get; set; } = WeaponClass.Undefined;
    [ProtoMember(20)]
    public WeaponClass AmmoClass { get; set; } = WeaponClass.Undefined;
    [ProtoMember(21)]
    public float TotalInertia { get; set; } = 0;
    [ProtoMember(22)]
    public float CenterOfMass { get; set; } = 0;
    [ProtoMember(23)]
    public Vec3 CenterOfMass3D { get; set; } = Vec3.Zero;
    [ProtoMember(24)]
    public float SwingDamageFactor { get; set; } = 0;
    [ProtoMember(25)]
    public float ThrustDamageFactor { get; set; } = 0;
    [ProtoMember(26)]
    public int Handling { get; set; } = 0;
    [ProtoMember(27)]
    public float SweetSpotReach { get; set; } = 0;
    [ProtoMember(28)]
    public string TrailParticleName { get; set; } = "";
    [ProtoMember(29)]
    public MatrixFrame StickingFrame { get; set; } = MatrixFrame.Zero;
    [ProtoMember(30)]
    public Vec3 AmmoOffset { get; set; } = Vec3.Zero;
    [ProtoMember(31)]
    public short MaxDataValue { get; set; } = 0;
    [ProtoMember(32)]
    public MatrixFrame Frame { get; set; } = MatrixFrame.Zero;
    [ProtoMember(33)]
    public Vec3 RotationSpeed { get; set; } = Vec3.Zero;
    [ProtoMember(34)]
    public short ReloadPhaseCount { get; set; } = 0;
    [ProtoMember(35)]
    public WeaponFlags WeaponFlags { get; set; } = 0UL;

    public WeaponComponentDataSurrogate(WeaponComponentData weaponComponentData)
    {
        if (weaponComponentData == null) return;

        WeaponTier = weaponComponentData.WeaponTier;
        WeaponDescriptionId = weaponComponentData.WeaponDescriptionId;
        BodyArmor = weaponComponentData.BodyArmor;
        PhysicsMaterial = weaponComponentData.PhysicsMaterial;
        FlyingSoundCode = weaponComponentData.FlyingSoundCode;
        PassbySoundCode = weaponComponentData.PassbySoundCode;
        ItemUsage = weaponComponentData.ItemUsage;
        ThrustSpeed = weaponComponentData.ThrustSpeed;
        SwingSpeed = weaponComponentData.SwingSpeed;
        MissileSpeed = weaponComponentData.MissileSpeed;
        WeaponLength = weaponComponentData.WeaponLength;
        WeaponBalance = weaponComponentData.WeaponBalance;
        ThrustDamage = weaponComponentData.ThrustDamage;
        ThrustDamageType = weaponComponentData.ThrustDamageType;
        SwingDamage = weaponComponentData.SwingDamage;
        SwingDamageType = weaponComponentData.SwingDamageType;
        FireDamage = weaponComponentData.FireDamage;
        Accuracy = weaponComponentData.Accuracy;
        WeaponClass = weaponComponentData.WeaponClass;
        AmmoClass = weaponComponentData.AmmoClass;
        TotalInertia = weaponComponentData.TotalInertia;
        CenterOfMass = weaponComponentData.CenterOfMass;
        CenterOfMass3D = weaponComponentData.CenterOfMass3D;
        SwingDamageFactor = weaponComponentData.SwingDamageFactor;
        ThrustDamageFactor = weaponComponentData.ThrustDamageFactor;
        Handling = weaponComponentData.Handling;
        SweetSpotReach = weaponComponentData.SweetSpotReach;
        TrailParticleName = weaponComponentData.TrailParticleName;
        StickingFrame = weaponComponentData.StickingFrame;
        AmmoOffset = weaponComponentData.AmmoOffset;
        MaxDataValue = weaponComponentData.MaxDataValue;
        Frame = weaponComponentData.Frame;
        RotationSpeed = weaponComponentData.RotationSpeed;
        ReloadPhaseCount = weaponComponentData.ReloadPhaseCount;
        WeaponFlags = weaponComponentData.WeaponFlags;
    }

    public static implicit operator WeaponComponentDataSurrogate(WeaponComponentData weaponComponentData)
    {
        return new WeaponComponentDataSurrogate(weaponComponentData);
    }

    public static implicit operator WeaponComponentData(WeaponComponentDataSurrogate surrogate)
    {
        var weaponComponentData = new WeaponComponentData(
            null, // Item isn't used, don't know why this is an argument
            surrogate.WeaponClass,
            surrogate.WeaponFlags
        )
        {
            WeaponTier = surrogate.WeaponTier,
            WeaponDescriptionId = surrogate.WeaponDescriptionId,
            BodyArmor = surrogate.BodyArmor,
            PhysicsMaterial = surrogate.PhysicsMaterial,
            FlyingSoundCode = surrogate.FlyingSoundCode,
            PassbySoundCode = surrogate.PassbySoundCode,
            ItemUsage = surrogate.ItemUsage,
            ThrustSpeed = surrogate.ThrustSpeed,
            SwingSpeed = surrogate.SwingSpeed,
            MissileSpeed = surrogate.MissileSpeed,
            WeaponLength = surrogate.WeaponLength,
            WeaponBalance = surrogate.WeaponBalance,
            ThrustDamage = surrogate.ThrustDamage,
            ThrustDamageType = surrogate.ThrustDamageType,
            SwingDamage = surrogate.SwingDamage,
            SwingDamageType = surrogate.SwingDamageType,
            FireDamage = surrogate.FireDamage,
            Accuracy = surrogate.Accuracy,
            WeaponClass = surrogate.WeaponClass,
            AmmoClass = surrogate.AmmoClass,
            TotalInertia = surrogate.TotalInertia,
            CenterOfMass = surrogate.CenterOfMass,
            CenterOfMass3D = surrogate.CenterOfMass3D,
            SwingDamageFactor = surrogate.SwingDamageFactor,
            ThrustDamageFactor = surrogate.ThrustDamageFactor,
            Handling = surrogate.Handling,
            SweetSpotReach = surrogate.SweetSpotReach,
            TrailParticleName = surrogate.TrailParticleName,
            StickingFrame = surrogate.StickingFrame,
            AmmoOffset = surrogate.AmmoOffset,
            MaxDataValue = surrogate.MaxDataValue,
            Frame = surrogate.Frame,
            RotationSpeed = surrogate.RotationSpeed,
            ReloadPhaseCount = surrogate.ReloadPhaseCount
        };

        return weaponComponentData;
    }
}

