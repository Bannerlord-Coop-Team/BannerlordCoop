using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships;

/// <summary>
/// Auto sync for campaign <see cref="Ship"/> state.
/// </summary>
internal class ShipSync : IAutoSync
{
    public ShipSync(AutoSyncRegistry autoSyncRegistry)
    {
        // The setter keeps both owners' PartyBase ship lists in step on every machine.
        autoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.Owner)));

        // Raw fields, so a client never re-clamps against its own MaxHitPoints. Coalesced because sea damage,
        // storms and simulated battle rounds hit whole fleets at once; mission snapshots are unregistered.
        autoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship._hitPoints)),
            coalesce: true, suppressClientDiagnosticWhenUnregistered: true);
        autoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship._sailHitPoints)),
            coalesce: true, suppressClientDiagnosticWhenUnregistered: true);

        autoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship._name)));

        autoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsInvulnerable)));
        autoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsTradeable)));
        autoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsUsedByQuest)));
        autoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.CustomSailPatternId)));
    }
}
