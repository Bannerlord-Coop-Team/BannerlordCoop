using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier
{
    class MapSpeedResolver
    {
        public static CampaignTimeControlMode Resolve(
            CampaignTimeControlMode currentTimeControlMode,
            bool canStop)
        {

            switch (currentTimeControlMode)
            {
                case CampaignTimeControlMode.Stop:
                    return canStop ? currentTimeControlMode : CampaignTimeControlMode.UnstoppablePlay;
                case CampaignTimeControlMode.StoppablePlay:
                    return CampaignTimeControlMode.UnstoppablePlay;
                case CampaignTimeControlMode.StoppableFastForward:
                    return CampaignTimeControlMode.UnstoppableFastForward;
                default:
                    return currentTimeControlMode;
            }

        }

    }
}
