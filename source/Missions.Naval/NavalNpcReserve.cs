using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>One side's NPC reserve on this client: its troop supplier and the parties in it no player owns that still have troops.</summary>
public readonly struct NavalNpcReserve
{
    public readonly CoopTroopSupplier Supplier;
    public readonly MBList<MapEventParty> Parties;

    // Holds the side's supplier, null before the side has one, with its AI parties.
    public NavalNpcReserve(CoopTroopSupplier supplier, MBList<MapEventParty> parties)
    {
        Supplier = supplier;
        Parties = parties;
    }

    /// <summary>Whether the server's reserve for the side reached this client.</summary>
    public bool IsPopulated => Supplier?.IsPopulated == true;

    /// <summary>How many AI parties in the reserve still have troops.</summary>
    public int NpcParties => Parties?.Count ?? 0;
}
