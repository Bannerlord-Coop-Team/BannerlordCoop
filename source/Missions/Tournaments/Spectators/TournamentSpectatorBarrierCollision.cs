using System;
using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

public static class TournamentSpectatorBarrierCollision
{
    private const float AgentRadius = 0.55f;

    public static bool CrossesBarrier(
        Vec3 previous,
        Vec3 current,
        TournamentSpectatorBarrierData barrier)
    {
        return TryConstrain(previous, current, barrier, out _);
    }

    public static bool TryConstrain(
        Vec3 previous,
        Vec3 current,
        TournamentSpectatorBarrierData barrier,
        out Vec3 constrained)
    {
        constrained = current;
        float minimumZ = barrier.Position.z - 0.5f;
        float maximumZ = barrier.Position.z + barrier.Scale.z + 0.5f;
        if (Math.Max(previous.z, current.z) < minimumZ ||
            Math.Min(previous.z, current.z) > maximumZ)
        {
            return false;
        }

        float cosine = (float)Math.Cos(barrier.Rotation);
        float sine = (float)Math.Sin(barrier.Rotation);
        float previousDeltaX = previous.x - barrier.Position.x;
        float previousDeltaY = previous.y - barrier.Position.y;
        float currentDeltaX = current.x - barrier.Position.x;
        float currentDeltaY = current.y - barrier.Position.y;
        float previousLocalX = cosine * previousDeltaX + sine * previousDeltaY;
        float previousLocalY = -sine * previousDeltaX + cosine * previousDeltaY;
        float currentLocalX = cosine * currentDeltaX + sine * currentDeltaY;
        float currentLocalY = -sine * currentDeltaX + cosine * currentDeltaY;
        if (previousLocalY * currentLocalY > 0f) return false;

        float denominator = previousLocalY - currentLocalY;
        if (Math.Abs(denominator) < 0.0001f) return false;
        float intersection = previousLocalY / denominator;
        if (intersection < 0f || intersection > 1f) return false;

        float intersectionX = previousLocalX + (currentLocalX - previousLocalX) * intersection;
        float intersectionZ = previous.z + (current.z - previous.z) * intersection;
        if (Math.Abs(intersectionX) > barrier.Scale.x * 0.5f + 0.5f ||
            intersectionZ < minimumZ ||
            intersectionZ > maximumZ)
        {
            return false;
        }

        float side = previousLocalY > 0f || previousLocalY == 0f && currentLocalY < 0f ? 1f : -1f;
        float constrainedLocalY = side * AgentRadius;
        constrained = new Vec3(
            barrier.Position.x + cosine * currentLocalX - sine * constrainedLocalY,
            barrier.Position.y + sine * currentLocalX + cosine * constrainedLocalY,
            current.z);
        return true;
    }
}