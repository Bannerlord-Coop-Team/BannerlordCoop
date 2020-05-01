using Coop.Game.Patch;
using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    internal class GameEnvironment : IEnvironment
    {
        public CampaignTimeControlMode TimeControlMode
        {
            get => Campaign.Current.TimeControlMode;
            set => TimeControl.SetForced_Campaign_TimeControlMode(value);
        }
    }
}
