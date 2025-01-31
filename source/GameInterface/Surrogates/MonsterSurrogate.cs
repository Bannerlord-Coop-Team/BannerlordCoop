using System;
using System.Collections.Generic;
using System.Text;
using Common.Util;
using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;
[ProtoContract]
internal struct MonsterSurrogate
{
    [ProtoMember(1)]
    public string BaseMonster { get; set; }
    [ProtoMember(2)]
    public float BodyCapsuleRadius { get; set; }
    [ProtoMember(3)]
    public Vec3 BodyCapsulePoint1 { get; set; }
    [ProtoMember(4)]
    public Vec3 BodyCapsulePoint2 { get; set; }
    [ProtoMember(5)]
    public float CrouchedBodyCapsuleRadius { get; set; }
    [ProtoMember(6)]
    public Vec3 CrouchedBodyCapsulePoint1 { get; set; }
    [ProtoMember(7)]
    public Vec3 CrouchedBodyCapsulePoint2 { get; set; }
    [ProtoMember(8)]
    public AgentFlag Flags { get; set; }
    [ProtoMember(9)]
    public int Weight { get; set; }
    [ProtoMember(10)]
    public int HitPoints { get; set; }
    [ProtoMember(11)]
    public string ActionSetCode { get; set; }
    [ProtoMember(12)]
    public string FemaleActionSetCode { get; set; }
    [ProtoMember(13)]
    public int NumPaces { get; set; }
    [ProtoMember(14)]
    public string MonsterUsage { get; set; }
    [ProtoMember(15)]
    public float WalkingSpeedLimit { get; set; }
    [ProtoMember(16)]
    public float CrouchWalkingSpeedLimit { get; set; }
    [ProtoMember(17)]
    public float JumpAcceleration { get; set; }
    [ProtoMember(18)]
    public float AbsorbedDamageRatio { get; set; }
    [ProtoMember(19)]
    public string SoundAndCollisionInfoClassName { get; set; }
    [ProtoMember(20)]
    public float RiderCameraHeightAdder { get; set; }
    [ProtoMember(21)]
    public float RiderBodyCapsuleHeightAdder { get; set; }
    [ProtoMember(22)]
    public float RiderBodyCapsuleForwardAdder { get; set; }
    [ProtoMember(23)]
    public float StandingChestHeight { get; set; }
    [ProtoMember(24)]
    public float StandingPelvisHeight { get; set; }
    [ProtoMember(25)]
    public float StandingEyeHeight { get; set; }
    [ProtoMember(26)]
    public float CrouchEyeHeight { get; set; }
    [ProtoMember(27)]
    public float MountedEyeHeight { get; set; }
    [ProtoMember(28)]
    public float RiderEyeHeightAdder { get; set; }
    [ProtoMember(29)]
    public Vec3 EyeOffsetWrtHead { get; set; }
    [ProtoMember(30)]
    public Vec3 FirstPersonCameraOffsetWrtHead { get; set; }
    [ProtoMember(31)]
    public float ArmLength { get; set; }
    [ProtoMember(32)]
    public float ArmWeight { get; set; }
    [ProtoMember(33)]
    public float JumpSpeedLimit { get; set; }
    [ProtoMember(34)]
    public float RelativeSpeedLimitForCharge { get; set; }
    [ProtoMember(35)]
    public int FamilyType { get; set; }
    [ProtoMember(36)]
    public sbyte[] IndicesOfRagdollBonesToCheckForCorpses { get; set; }
    [ProtoMember(37)]
    public sbyte[] RagdollFallSoundBoneIndices { get; set; }
    [ProtoMember(38)]
    public sbyte HeadLookDirectionBoneIndex { get; set; }
    [ProtoMember(39)]
    public sbyte SpineLowerBoneIndex { get; set; }
    [ProtoMember(40)]
    public sbyte SpineUpperBoneIndex { get; set; }
    [ProtoMember(41)]
    public sbyte ThoraxLookDirectionBoneIndex { get; set; }
    [ProtoMember(42)]
    public sbyte NeckRootBoneIndex { get; set; }
    [ProtoMember(43)]
    public sbyte PelvisBoneIndex { get; set; }
    [ProtoMember(44)]
    public sbyte RightUpperArmBoneIndex { get; set; }
    [ProtoMember(45)]
    public sbyte LeftUpperArmBoneIndex { get; set; }
    [ProtoMember(46)]
    public sbyte FallBlowDamageBoneIndex { get; set; }
    [ProtoMember(47)]
    public sbyte TerrainDecalBone0Index { get; set; }
    [ProtoMember(48)]
    public sbyte TerrainDecalBone1Index { get; set; }
    [ProtoMember(49)]
    public sbyte[] RagdollStationaryCheckBoneIndices { get; set; }
    [ProtoMember(50)]
    public sbyte[] MoveAdderBoneIndices { get; set; }
    [ProtoMember(51)]
    public sbyte[] SplashDecalBoneIndices { get; set; }
    [ProtoMember(52)]
    public sbyte[] BloodBurstBoneIndices { get; set; }
    [ProtoMember(53)]
    public sbyte MainHandBoneIndex { get; set; }
    [ProtoMember(54)]
    public sbyte OffHandBoneIndex { get; set; }
    [ProtoMember(55)]
    public sbyte MainHandItemBoneIndex { get; set; }
    [ProtoMember(56)]
    public sbyte OffHandItemBoneIndex { get; set; }
    [ProtoMember(57)]
    public sbyte MainHandItemSecondaryBoneIndex { get; set; }
    [ProtoMember(58)]
    public sbyte OffHandItemSecondaryBoneIndex { get; set; }
    [ProtoMember(59)]
    public sbyte OffHandShoulderBoneIndex { get; set; }
    [ProtoMember(60)]
    public sbyte HandNumBonesForIk { get; set; }
    [ProtoMember(61)]
    public sbyte PrimaryFootBoneIndex { get; set; }
    [ProtoMember(62)]
    public sbyte SecondaryFootBoneIndex { get; set; }
    [ProtoMember(63)]
    public sbyte RightFootIkEndEffectorBoneIndex { get; set; }
    [ProtoMember(64)]
    public sbyte LeftFootIkEndEffectorBoneIndex { get; set; }
    [ProtoMember(65)]
    public sbyte RightFootIkTipBoneIndex { get; set; }
    [ProtoMember(66)]
    public sbyte LeftFootIkTipBoneIndex { get; set; }
    [ProtoMember(67)]
    public sbyte FootNumBonesForIk { get; set; }
    [ProtoMember(68)]
    public Vec3 ReinHandleLeftLocalPosition { get; set; }
    [ProtoMember(69)]
    public Vec3 ReinHandleRightLocalPosition { get; set; }
    [ProtoMember(70)]
    public string ReinSkeleton { get; set; }
    [ProtoMember(71)]
    public string ReinCollisionBody { get; set; }
    [ProtoMember(72)]
    public sbyte FrontBoneToDetectGroundSlopeIndex { get; set; }
    [ProtoMember(73)]
    public sbyte BackBoneToDetectGroundSlopeIndex { get; set; }
    [ProtoMember(74)]
    public sbyte[] BoneIndicesToModifyOnSlopingGround { get; set; }
    [ProtoMember(75)]
    public sbyte BodyRotationReferenceBoneIndex { get; set; }
    [ProtoMember(76)]
    public sbyte RiderSitBoneIndex { get; set; }
    [ProtoMember(77)]
    public sbyte ReinHandleBoneIndex { get; set; }
    [ProtoMember(78)]
    public sbyte ReinCollision1BoneIndex { get; set; }
    [ProtoMember(79)]
    public sbyte ReinCollision2BoneIndex { get; set; }
    [ProtoMember(80)]
    public sbyte ReinHeadBoneIndex { get; set; }
    [ProtoMember(81)]
    public sbyte ReinHeadRightAttachmentBoneIndex { get; set; }
    [ProtoMember(82)]
    public sbyte ReinHeadLeftAttachmentBoneIndex { get; set; }
    [ProtoMember(83)]
    public sbyte ReinRightHandBoneIndex { get; set; }
    [ProtoMember(84)]
    public sbyte ReinLeftHandBoneIndex { get; set; }

    public MonsterSurrogate(Monster Monster)
    {
        if (Monster == null)
        {
            BaseMonster = "";
            BodyCapsuleRadius = -1;
            BodyCapsulePoint1 = ObjectHelper.SkipConstructor<Vec3>();
            BodyCapsulePoint2 = ObjectHelper.SkipConstructor<Vec3>();
            CrouchedBodyCapsuleRadius = -1;
            CrouchedBodyCapsulePoint1 = ObjectHelper.SkipConstructor<Vec3>();
            CrouchedBodyCapsulePoint2 = ObjectHelper.SkipConstructor<Vec3>();
            Flags = AgentFlag.None;
            Weight = -1;
            HitPoints = -1;
            ActionSetCode = "";
            FemaleActionSetCode = "";
            NumPaces = -1;
            MonsterUsage = "";
            WalkingSpeedLimit = -1;
            CrouchWalkingSpeedLimit = -1;
            JumpAcceleration = -1;
            AbsorbedDamageRatio = -1;
            SoundAndCollisionInfoClassName = "";
            RiderCameraHeightAdder = -1;
            RiderBodyCapsuleHeightAdder = -1;
            RiderBodyCapsuleForwardAdder = -1;
            StandingChestHeight = -1;
            StandingPelvisHeight = -1;
            StandingEyeHeight = -1;
            CrouchEyeHeight = -1;
            MountedEyeHeight = -1;
            RiderEyeHeightAdder = -1;
            EyeOffsetWrtHead = ObjectHelper.SkipConstructor<Vec3>();
            FirstPersonCameraOffsetWrtHead = ObjectHelper.SkipConstructor<Vec3>();
            ArmLength = -1;
            ArmWeight = -1;
            JumpSpeedLimit = -1;
            RelativeSpeedLimitForCharge = -1;
            FamilyType = -1;
            IndicesOfRagdollBonesToCheckForCorpses = new sbyte[0];
            RagdollFallSoundBoneIndices = new sbyte[0];
            HeadLookDirectionBoneIndex = -1;
            SpineLowerBoneIndex = -1;
            SpineUpperBoneIndex = -1;
            ThoraxLookDirectionBoneIndex = -1;
            NeckRootBoneIndex = -1;
            PelvisBoneIndex = -1;
            RightUpperArmBoneIndex = -1;
            LeftUpperArmBoneIndex = -1;
            FallBlowDamageBoneIndex = -1;
            TerrainDecalBone0Index = -1;
            TerrainDecalBone1Index = -1;
            RagdollStationaryCheckBoneIndices = new sbyte[0];
            MoveAdderBoneIndices = new sbyte[0];
            SplashDecalBoneIndices = new sbyte[0];
            BloodBurstBoneIndices = new sbyte[0];
            MainHandBoneIndex = -1;
            OffHandBoneIndex = -1;
            MainHandItemBoneIndex = -1;
            OffHandItemBoneIndex = -1;
            MainHandItemSecondaryBoneIndex = -1;
            OffHandItemSecondaryBoneIndex = -1;
            OffHandShoulderBoneIndex = -1;
            HandNumBonesForIk = -1;
            PrimaryFootBoneIndex = -1;
            SecondaryFootBoneIndex = -1;
            RightFootIkEndEffectorBoneIndex = -1;
            LeftFootIkEndEffectorBoneIndex = -1;
            RightFootIkTipBoneIndex = -1;
            LeftFootIkTipBoneIndex = -1;
            FootNumBonesForIk = -1;
            ReinHandleLeftLocalPosition = ObjectHelper.SkipConstructor<Vec3>();
            ReinHandleRightLocalPosition = ObjectHelper.SkipConstructor<Vec3>();
            ReinSkeleton = "";
            ReinCollisionBody = "";
            FrontBoneToDetectGroundSlopeIndex = -1;
            BackBoneToDetectGroundSlopeIndex = -1;
            BoneIndicesToModifyOnSlopingGround = new sbyte[0];
            BodyRotationReferenceBoneIndex = -1;
            RiderSitBoneIndex = -1;
            ReinHandleBoneIndex = -1;
            ReinCollision1BoneIndex = -1;
            ReinCollision2BoneIndex = -1;
            ReinHeadBoneIndex = -1;
            ReinHeadRightAttachmentBoneIndex = -1;
            ReinHeadLeftAttachmentBoneIndex = -1;
            ReinRightHandBoneIndex = -1;
            ReinLeftHandBoneIndex = -1;
        }
        else
        {
            BaseMonster = Monster.BaseMonster;
            BodyCapsuleRadius = Monster.BodyCapsuleRadius;
            BodyCapsulePoint1 = Monster.BodyCapsulePoint1;
            BodyCapsulePoint2 = Monster.BodyCapsulePoint2;
            CrouchedBodyCapsuleRadius = Monster.CrouchedBodyCapsuleRadius;
            CrouchedBodyCapsulePoint1 = Monster.CrouchedBodyCapsulePoint1;
            CrouchedBodyCapsulePoint2 = Monster.CrouchedBodyCapsulePoint2;
            Flags = Monster.Flags;
            Weight = Monster.Weight;
            HitPoints = Monster.HitPoints;
            ActionSetCode = Monster.ActionSetCode;
            FemaleActionSetCode = Monster.FemaleActionSetCode;
            NumPaces = Monster.NumPaces;
            MonsterUsage = Monster.MonsterUsage;
            WalkingSpeedLimit = Monster.WalkingSpeedLimit;
            CrouchWalkingSpeedLimit = Monster.CrouchWalkingSpeedLimit;
            JumpAcceleration = Monster.JumpAcceleration;
            AbsorbedDamageRatio = Monster.AbsorbedDamageRatio;
            SoundAndCollisionInfoClassName = Monster.SoundAndCollisionInfoClassName;
            RiderCameraHeightAdder = Monster.RiderCameraHeightAdder;
            RiderBodyCapsuleHeightAdder = Monster.RiderBodyCapsuleHeightAdder;
            RiderBodyCapsuleForwardAdder = Monster.RiderBodyCapsuleForwardAdder;
            StandingChestHeight = Monster.StandingChestHeight;
            StandingPelvisHeight = Monster.StandingPelvisHeight;
            StandingEyeHeight = Monster.StandingEyeHeight;
            CrouchEyeHeight = Monster.CrouchEyeHeight;
            MountedEyeHeight = Monster.MountedEyeHeight;
            RiderEyeHeightAdder = Monster.RiderEyeHeightAdder;
            EyeOffsetWrtHead = Monster.EyeOffsetWrtHead;
            FirstPersonCameraOffsetWrtHead = Monster.FirstPersonCameraOffsetWrtHead;
            ArmLength = Monster.ArmLength;
            ArmWeight = Monster.ArmWeight;
            JumpSpeedLimit = Monster.JumpSpeedLimit;
            RelativeSpeedLimitForCharge = Monster.RelativeSpeedLimitForCharge;
            FamilyType = Monster.FamilyType;
            IndicesOfRagdollBonesToCheckForCorpses = Monster.IndicesOfRagdollBonesToCheckForCorpses ?? new sbyte[0];
            RagdollFallSoundBoneIndices = Monster.RagdollFallSoundBoneIndices ?? new sbyte[0];
            HeadLookDirectionBoneIndex = Monster.HeadLookDirectionBoneIndex;
            SpineLowerBoneIndex = Monster.SpineLowerBoneIndex;
            SpineUpperBoneIndex = Monster.SpineUpperBoneIndex;
            ThoraxLookDirectionBoneIndex = Monster.ThoraxLookDirectionBoneIndex;
            NeckRootBoneIndex = Monster.NeckRootBoneIndex;
            PelvisBoneIndex = Monster.PelvisBoneIndex;
            RightUpperArmBoneIndex = Monster.RightUpperArmBoneIndex;
            LeftUpperArmBoneIndex = Monster.LeftUpperArmBoneIndex;
            FallBlowDamageBoneIndex = Monster.FallBlowDamageBoneIndex;
            TerrainDecalBone0Index = Monster.TerrainDecalBone0Index;
            TerrainDecalBone1Index = Monster.TerrainDecalBone1Index;
            RagdollStationaryCheckBoneIndices = Monster.RagdollStationaryCheckBoneIndices ?? new sbyte[0];
            MoveAdderBoneIndices = Monster.MoveAdderBoneIndices ?? new sbyte[0];
            SplashDecalBoneIndices = Monster.SplashDecalBoneIndices ?? new sbyte[0];
            BloodBurstBoneIndices = Monster.BloodBurstBoneIndices ?? new sbyte[0];
            MainHandBoneIndex = Monster.MainHandBoneIndex;
            OffHandBoneIndex = Monster.OffHandBoneIndex;
            MainHandItemBoneIndex = Monster.MainHandItemBoneIndex;
            OffHandItemBoneIndex = Monster.OffHandItemBoneIndex;
            MainHandItemSecondaryBoneIndex = Monster.MainHandItemSecondaryBoneIndex;
            OffHandItemSecondaryBoneIndex = Monster.OffHandItemSecondaryBoneIndex;
            OffHandShoulderBoneIndex = Monster.OffHandShoulderBoneIndex;
            HandNumBonesForIk = Monster.HandNumBonesForIk;
            PrimaryFootBoneIndex = Monster.PrimaryFootBoneIndex;
            SecondaryFootBoneIndex = Monster.SecondaryFootBoneIndex;
            RightFootIkEndEffectorBoneIndex = Monster.RightFootIkEndEffectorBoneIndex;
            LeftFootIkEndEffectorBoneIndex = Monster.LeftFootIkEndEffectorBoneIndex;
            RightFootIkTipBoneIndex = Monster.RightFootIkTipBoneIndex;
            LeftFootIkTipBoneIndex = Monster.LeftFootIkTipBoneIndex;
            FootNumBonesForIk = Monster.FootNumBonesForIk;
            ReinHandleLeftLocalPosition = Monster.ReinHandleLeftLocalPosition;
            ReinHandleRightLocalPosition = Monster.ReinHandleRightLocalPosition;
            ReinSkeleton = Monster.ReinSkeleton;
            ReinCollisionBody = Monster.ReinCollisionBody;
            FrontBoneToDetectGroundSlopeIndex = Monster.FrontBoneToDetectGroundSlopeIndex;
            BackBoneToDetectGroundSlopeIndex = Monster.BackBoneToDetectGroundSlopeIndex;
            BoneIndicesToModifyOnSlopingGround = Monster.BoneIndicesToModifyOnSlopingGround;
            BodyRotationReferenceBoneIndex = Monster.BodyRotationReferenceBoneIndex;
            RiderSitBoneIndex = Monster.RiderSitBoneIndex;
            ReinHandleBoneIndex = Monster.ReinHandleBoneIndex;
            ReinCollision1BoneIndex = Monster.ReinCollision1BoneIndex;
            ReinCollision2BoneIndex = Monster.ReinCollision2BoneIndex;
            ReinHeadBoneIndex = Monster.ReinHeadBoneIndex;
            ReinHeadRightAttachmentBoneIndex = Monster.ReinHeadRightAttachmentBoneIndex;
            ReinHeadLeftAttachmentBoneIndex = Monster.ReinHeadLeftAttachmentBoneIndex;
            ReinRightHandBoneIndex = Monster.ReinRightHandBoneIndex;
            ReinLeftHandBoneIndex = Monster.ReinLeftHandBoneIndex;
        }
    }

    public static implicit operator MonsterSurrogate(Monster Monster)
    {
        return new MonsterSurrogate(Monster);
    }

    public static implicit operator Monster(MonsterSurrogate MonsterSurrogate)
    {
        return new Monster();
    }
}
