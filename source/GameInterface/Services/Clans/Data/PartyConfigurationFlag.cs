using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Data;

/// <summary>
/// The per-leader party commands on the clan screen.
/// Each one is a <see cref="Hero"/> property backed by its party configuration.
/// </summary>
public enum PartyConfigurationFlag
{
    CanJoinArmy,
    CanRaid,
    CanDonateTroopsToGarrison,
    CanHaveFleet,
}
