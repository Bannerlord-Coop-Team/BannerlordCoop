using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command carrying the full server-side character roster of every location in a settlement.
/// Sent when a player party enters a settlement so clients (including late joiners, whose
/// rosters start empty because the character list is not part of the save) can reconcile
/// their synced entries to the server state.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLocationRosterSnapshot : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly LocationCharacterData[] Entries;

    public NetworkLocationRosterSnapshot(string settlementId, LocationCharacterData[] entries)
    {
        SettlementId = settlementId;
        Entries = entries;
    }
}
