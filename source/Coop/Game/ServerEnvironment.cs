using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class ServerEnvironment : IEnvironment
    {
        public CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        public CampaignTimeControlMode TimeControlMode { get; set; } = CampaignTimeControlMode.Stop;
    }
}
