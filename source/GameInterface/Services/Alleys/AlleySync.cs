using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys;

public class AlleySync : IAutoSync
{
    public AlleySync(AutoSyncRegistry registry) 
    {
        // Fields
        // _name, _settlement and _tag are fixed when the alley is generated and never change.
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._name)));
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._settlement)));
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._tag)));

        // _owner is intentionally not synced as a field. Owner changes are replicated by replaying
        // Alley.SetOwner on each client (see AlleyPatches / AlleyHandler) so the derived State and the
        // owner's OwnedAlleys list are reproduced too, which a raw field set cannot do.

        // Properties

        // Targetmethods
    }
}
