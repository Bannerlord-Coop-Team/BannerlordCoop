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
    /// Replicates war declarations. The server publishes the change and runs the original action
    /// live (so the stance flip and its campaign event happen authoritatively); clients are blocked
    /// from originating and instead re-apply from the network message.
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarAction), "ApplyInternal")]
    internal class DeclareWarActionPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<DeclareWarActionPatch>();

        public static bool Prefix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                // Expected on clients (player hostility/crime, or replaying a synced AI decision):
                // block the local apply; the authoritative change arrives via the stance sync.
                Logger.Debug("Ignoring client-originated {action}; awaiting server replication.", nameof(DeclareWarAction));
                return false;
            }

            MessageBroker.Instance.Publish(faction1, new FactionWarDeclared(faction1, faction2, (int)declareWarDetail));
            return true;
        }
    }
}
