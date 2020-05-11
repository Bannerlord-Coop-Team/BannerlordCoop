using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        public CampaignTimeControlMode TimeControlMode { get; set; } = CampaignTimeControlMode.Stop;
    }
}
