using GameInterface.Services.ViewData.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.ViewData;

/// <summary>
/// ViewDataTrackerCampaignBehavior holds the UI state of only one player
/// Inventory locks and sort preferences are kept separately in InventoryPlayerData
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class ViewDataPlayerData
{
    // Dictionary<PlayerHeroId, PlayerViewData>
    [ProtoMember(1)]
    public Dictionary<string, PlayerViewData> PlayerViewData { get; }

    public ViewDataPlayerData(
        Dictionary<string, PlayerViewData> playerViewData)
    {
        PlayerViewData = playerViewData ?? new();
    }
}
