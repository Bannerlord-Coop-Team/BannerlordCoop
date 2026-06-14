using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Stances.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Replicates peace settlements, including the daily tribute attached to the stance. The
    /// server publishes the change and runs the original action live; clients are blocked from
    /// originating and re-apply (with tribute) from the network message.
    /// </summary>
    [HarmonyPatch(typeof(MakePeaceAction), "ApplyInternal")]
    internal class MakePeaceActionPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MakePeaceActionPatch>();

        public static bool Prefix(IFaction faction1, IFaction faction2, int dailyTributeFrom1To2, int dailyTributeDuration, MakePeaceAction.MakePeaceDetail detail)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                // Expected on clients (or replaying a synced AI decision): block the local apply;
                // the authoritative change arrives via the stance sync.
                Logger.Debug("Ignoring client-originated {action}; awaiting server replication.", nameof(MakePeaceAction));
                return false;
            }

            MessageBroker.Instance.Publish(faction1,
                new FactionPeaceMade(faction1, faction2, dailyTributeFrom1To2, dailyTributeDuration, (int)detail));
            return true;
        }
    }
}
