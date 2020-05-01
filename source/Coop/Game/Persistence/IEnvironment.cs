using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironment
    {
        CampaignTimeControlMode TimeControlMode { get; set; }
    }
}
