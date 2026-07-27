using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IPuppetMountStateRepairer
{
    void PrepareForAiControl(Agent mount);

    void PreserveRiderlessPuppet(Agent mount);

    void RepairAfterRiderDeath(Agent mount);
}

public class PuppetMountStateRepairer : IPuppetMountStateRepairer
{
    public void PrepareForAiControl(Agent mount)
    {
        if (mount == null
            || mount.Controller == AgentControllerType.AI
            || mount.CommonAIComponent == null)
        {
            return;
        }

        mount.RemoveComponent(mount.CommonAIComponent);
    }

    public void PreserveRiderlessPuppet(Agent mount)
    {
        if (mount == null
            || !mount.IsActive()
            || mount.RiderAgent != null
            || mount.Controller != AgentControllerType.None)
        {
            return;
        }

        if (mount.CommonAIComponent == null)
            mount.AddComponent(new CommonAIComponent(mount));
    }

    public void RepairAfterRiderDeath(Agent mount)
    {
        PreserveRiderlessPuppet(mount);
        if (mount?.CommonAIComponent == null
            || !mount.IsActive()
            || mount.RiderAgent != null)
        {
            return;
        }

        mount.CommonAIComponent.OnMountUnreserved();
    }
}
