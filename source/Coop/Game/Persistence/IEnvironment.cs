using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public static class Environment
    {
        public static IEnvironment Current = null; // TODO: Should be injected
    }
    public interface IEnvironment
    {
        CampaignTimeControlMode TimeControlMode { get; set; }
    }
}
