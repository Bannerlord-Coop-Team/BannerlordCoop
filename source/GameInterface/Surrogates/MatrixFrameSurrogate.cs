using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct MatrixFrameSurrogate
{
    [ProtoMember(1)]
    public Mat3 Rotation { get; set; }

    [ProtoMember(2)]
    public Vec3 Origin { get; set; }

    public MatrixFrameSurrogate(MatrixFrame matrixFrame)
    {
        Rotation = matrixFrame.rotation;
        Origin = matrixFrame.origin;
    }

    public static implicit operator MatrixFrameSurrogate(MatrixFrame matrixFrame)
    {
        return new MatrixFrameSurrogate(matrixFrame);
    }

    public static implicit operator MatrixFrame(MatrixFrameSurrogate surrogate)
    {
        return new MatrixFrame(surrogate.Rotation, surrogate.Origin);
    }
}

