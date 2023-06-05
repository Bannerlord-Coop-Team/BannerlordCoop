using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    public static class MobilePartyAiExtensions
    {
        private static readonly FieldInfo MobilePartyAi_mobileParty = typeof(MobilePartyAi)
            .GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance);

        public static MobileParty GetMobileParty(this MobilePartyAi ai)
        {
            return MobilePartyAi_mobileParty.GetValue(ai) as MobileParty;
        }

        /// <summary>
        /// Determines whether the party entity is controlled locally.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Requires <see cref="MobilePartyAi.DoNotMakeNewDecisions"/> to be set to false on all non-controlled 
        /// entities. It may return a false negative for native uses of this field. <br/>
        /// 
        /// The MainParty entity is always assumed to be controlled locally.
        /// </para>
        /// Note: This method exists to isolate the relevant logic as the current approach may be unsustainable.
        /// </remarks>
        public static bool IsControlled(this MobilePartyAi ai)
        {
            return ai.GetMobileParty().IsMainParty || !ai.DoNotMakeNewDecisions;
        }
    }
}
