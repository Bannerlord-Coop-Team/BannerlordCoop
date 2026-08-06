using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAnimationActionCountProvider
{
    int GetActionCount();
}

public sealed class AnimationActionCountProvider :
    IAnimationActionCountProvider
{
    public int GetActionCount()
    {
        return MBAnimation.GetNumActionCodes();
    }
}
