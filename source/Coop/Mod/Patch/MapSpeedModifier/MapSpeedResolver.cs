using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier
{

    /// <summary>
    ///     Campaign map time speed resolver class.
    /// </summary>
    class MapSpeedResolver
    {
        /// <summary>
        ///     <para>
        ///         Given a <c>CampaignTimeControlMode</c> returns a new one. It's useful to override consistently current 
        ///         <c>CampaignTimeControlMode</c> through all method users.
        ///     </para>
        ///     <para>
        ///         More about it on <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/108">issue #108</see>
        ///     </para>
        /// </summary>
        /// <param name="timeControlMode">
        ///     Any <c>CampaignTimeControlMode</c> which method user desires will suffice. 
        ///     Most useful being the current one.
        /// </param>
        /// <param name="canStop">
        ///     Useful in cases when method user does or doesn't want the new <c>CampaignTimeControlMode</c> to be
        ///     equal to <c>CampaignTimeControlMode.Stop</c>
        /// </param>
        /// <returns>Resolved <c>CampaignTimeControlMode</c></returns>
        public static CampaignTimeControlMode Resolve(
            CampaignTimeControlMode timeControlMode,
            bool canStop)
        {

            switch (timeControlMode)
            {
                case CampaignTimeControlMode.Stop:
                    return canStop ? timeControlMode : CampaignTimeControlMode.UnstoppablePlay;
                case CampaignTimeControlMode.StoppablePlay:
                    return CampaignTimeControlMode.UnstoppablePlay;
                case CampaignTimeControlMode.StoppableFastForward:
                    return CampaignTimeControlMode.UnstoppableFastForward;
                default:
                    return timeControlMode;
            }

        }

    }

}
