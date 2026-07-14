using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Shared <see cref="MissionInitializerRecord"/> boilerplate the per-type battle mission initializers
/// (field, village raid, siege) each set identically. Extracted so the difficulty multipliers,
/// campaign-mode flag, server-fed atmosphere, and server-fed terrain seed have one definition instead of
/// three near-duplicate inline copies. Each initializer calls only the pieces it set before, so behavior
/// stays byte-identical to the pre-extraction inline construction.
/// </summary>
internal static class RecordDefaults
{
    /// <summary>The difficulty-scaled friendly-fire multipliers both the field and siege records set.</summary>
    public static void ApplyDamageMultipliers(MissionInitializerRecord record)
    {
        float receivedDamageMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        record.DamageToFriendsMultiplier = receivedDamageMultiplier;
        record.DamageFromPlayerToFriendsMultiplier = receivedDamageMultiplier;
    }

    /// <summary>The campaign-mode flag plus the atmosphere every mission plays in (all three records).</summary>
    public static void ApplyCampaignMode(MissionInitializerRecord record, AtmosphereInfo atmosphereOnCampaign)
    {
        record.PlayingInCampaignMode = true;
        record.AtmosphereOnCampaign = atmosphereOnCampaign;
    }

    /// <summary>The server-rolled terrain seed carried in the start message (field and raid records).</summary>
    public static void ApplyTerrainSeed(MissionInitializerRecord record, int randomTerrainSeed)
    {
        record.RandomTerrainSeed = randomTerrainSeed;
    }
}
