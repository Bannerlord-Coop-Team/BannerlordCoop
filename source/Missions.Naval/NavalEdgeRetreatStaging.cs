#if DEBUG
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>Places a hull just inside a measured boundary, facing out to sea.</summary>
public interface INavalEdgeRetreatStaging
{
    /// <summary>Preserves water height and puts the origin within vanilla's minimum 0.5 m retreat radius.</summary>
    MatrixFrame AtEdge(Vec3 origin, Vec2 boundary);
}

/// <inheritdoc cref="INavalEdgeRetreatStaging"/>
public sealed class NavalEdgeRetreatStaging : INavalEdgeRetreatStaging
{
    /// <inheritdoc/>
    public MatrixFrame AtEdge(Vec3 origin, Vec2 boundary)
    {
        var outward = (boundary - origin.AsVec2).Normalized();
        var frame = MatrixFrame.Identity;
        frame.origin = (boundary - (outward * 0.25f)).ToVec3(origin.z);
        frame.rotation.f = outward.ToVec3();
        frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        return frame;
    }
}
#endif
