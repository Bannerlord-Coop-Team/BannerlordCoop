using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;
[ProtoContract]
internal struct SkeletonScaleSurrogate
{
    [ProtoMember(1)]
    public string SkeletonModel { get; private set; }
    [ProtoMember(2)]
    public Vec3 MountSitBoneScale { get; private set; }
    [ProtoMember(3)]
    public float MountRadiusAdder { get; private set; }
    [ProtoMember(4)]
    public Vec3[] Scales { get; private set; }
    [ProtoMember(5)]
    public List<string> BoneNames { get; private set; }
    [ProtoMember(6)]
    public sbyte[] BoneIndices { get; private set; }

    public SkeletonScaleSurrogate(SkeletonScale SkeletonScale)
    {
        if (SkeletonScale == null)
        {
            SkeletonModel = "";
            MountSitBoneScale = ObjectHelper.SkipConstructor<Vec3>();
            MountRadiusAdder = -1f;
            Scales = ObjectHelper.SkipConstructor<Vec3[]>();
            BoneNames = new List<string>() { "" };
            BoneIndices = new sbyte[] { 0 };
        }
        else
        {
            SkeletonModel = SkeletonScale.SkeletonModel;
            MountSitBoneScale = SkeletonScale.MountSitBoneScale;
            MountRadiusAdder = SkeletonScale.MountRadiusAdder;
            Scales = SkeletonScale.Scales;
            BoneNames = SkeletonScale.BoneNames;
            BoneIndices = SkeletonScale.BoneIndices;
        }
    }

    public static implicit operator SkeletonScaleSurrogate(SkeletonScale SkeletonScale)
    {
        return new SkeletonScaleSurrogate(SkeletonScale);
    }

    public static implicit operator SkeletonScale(SkeletonScaleSurrogate SkeletonScale)
    {
        return new SkeletonScale();
    }
}
