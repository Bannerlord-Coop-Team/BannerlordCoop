#if DEBUG
using System;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>Defines deterministic selection priorities and movement evidence requirements.</summary>
public interface IClanLordMovementFixtureRules
{
    int LordPriority(string heroId);

    string ValidateMovement(double baselineTicks, double nowTicks,
        float startX, float startY, float currentX, float currentY,
        float targetX, float targetY, float actualTargetX, float actualTargetY,
        bool coherentBehavior, bool waiting, bool aiDisabled);
}

/// <summary>Rejects stale, stationary, or inconsistent movement samples.</summary>
public sealed class ClanLordMovementFixtureRules : IClanLordMovementFixtureRules
{
    public int LordPriority(string heroId)
    {
        switch (heroId)
        {
            case "lord_1_63_1": return 0;
            case "lord_1_74": return 1;
            case "lord_1_74_1": return 2;
            default: return 3;
        }
    }

    public string ValidateMovement(double baselineTicks, double nowTicks,
        float startX, float startY, float currentX, float currentY,
        float targetX, float targetY, float actualTargetX, float actualTargetY,
        bool coherentBehavior, bool waiting, bool aiDisabled)
    {
        if (!IsFinite(baselineTicks) || !IsFinite(nowTicks) ||
            !IsFinite(startX) || !IsFinite(startY) ||
            !IsFinite(currentX) || !IsFinite(currentY) ||
            !IsFinite(targetX) || !IsFinite(targetY) ||
            !IsFinite(actualTargetX) || !IsFinite(actualTargetY))
            return "Movement observation contains a non-finite timestamp or coordinate.";
        if (nowTicks <= baselineTicks)
            return "Campaign time has not advanced since the observation baseline.";
        if (!coherentBehavior)
            return "Party behavior does not match the fixture movement order.";
        if (waiting)
            return "Party is still waiting after the conversation transition.";
        if (aiDisabled)
            return "Party AI is still disabled after the conversation transition.";
        if (DistanceSquared(targetX, targetY, actualTargetX, actualTargetY) > 0.0001)
            return "Party movement target differs from the fixture target by more than 0.01 map units.";
        if (DistanceSquared(startX, startY, currentX, currentY) < 0.01)
            return "Party has moved less than 0.1 map units since the observation baseline.";
        if (DistanceSquared(currentX, currentY, targetX, targetY) < 0.25)
            return "Party is less than 0.5 map units from its target; capture an earlier movement sample.";

        return null;
    }

    private bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private double DistanceSquared(float firstX, float firstY, float secondX, float secondY)
    {
        double deltaX = (double)secondX - firstX;
        double deltaY = (double)secondY - firstY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
#endif
