using GameInterface.Services.Inventory.Data;
using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal sealed class PlayerPartyInteractionSession
{
    public string SessionId { get; }
    public string InitiatorPartyId { get; }
    public string ResponderPartyId { get; }
    public string InitiatorName { get; }
    public string ResponderName { get; }
    public NetPeer InitiatorPeer { get; }
    public NetPeer ResponderPeer { get; set; }
    public bool IsHostile { get; }
    public PlayerPartyInteractionVassalUnavailableReason VassalUnavailableReason { get; set; }
    public PlayerPartyInteractionProposal Proposal { get; set; }
    public bool HostileDemandConfirmed { get; set; }
    public bool InitiatorAcceptedTrade { get; set; }
    public bool ResponderAcceptedTrade { get; set; }
    public ItemRosterElementData[] InitiatorOfferedItems { get; set; } = new ItemRosterElementData[0];
    public ItemRosterElementData[] ResponderOfferedItems { get; set; } = new ItemRosterElementData[0];
    public int InitiatorOfferedGold { get; set; }
    public int ResponderOfferedGold { get; set; }
    public string[] InitiatorOfferedFiefs { get; set; } = new string[0];
    public string[] ResponderOfferedFiefs { get; set; } = new string[0];
    public TroopRosterElementData[] InitiatorOfferedPrisoners { get; set; } = new TroopRosterElementData[0];
    public TroopRosterElementData[] ResponderOfferedPrisoners { get; set; } = new TroopRosterElementData[0];
    public TroopRosterElementData[] InitiatorOfferedTroops { get; set; } = new TroopRosterElementData[0];
    public TroopRosterElementData[] ResponderOfferedTroops { get; set; } = new TroopRosterElementData[0];
    public bool InitiatorOfferedPeace { get; set; }
    public bool ResponderOfferedPeace { get; set; }

    public HashSet<PlayerPartyInteractionOption> InitiatorOptions { get; } = new HashSet<PlayerPartyInteractionOption>();
    public HashSet<PlayerPartyInteractionOption> InitiatorEnabledOptions { get; } = new HashSet<PlayerPartyInteractionOption>();

    public PlayerPartyInteractionSession(
        string sessionId,
        string initiatorPartyId,
        string responderPartyId,
        string initiatorName,
        string responderName,
        NetPeer initiatorPeer,
        bool isHostile = false)
    {
        SessionId = sessionId;
        InitiatorPartyId = initiatorPartyId;
        ResponderPartyId = responderPartyId;
        InitiatorName = initiatorName;
        ResponderName = responderName;
        InitiatorPeer = initiatorPeer;
        IsHostile = isHostile;
    }

    public bool ContainsParty(string partyId)
        => partyId == InitiatorPartyId || partyId == ResponderPartyId;

    public string GetOtherPartyId(string partyId)
        => partyId == InitiatorPartyId ? ResponderPartyId : InitiatorPartyId;

    public string GetOtherName(string partyId)
        => partyId == InitiatorPartyId ? ResponderName : InitiatorName;

    public bool IsInitiator(string partyId)
        => partyId == InitiatorPartyId;

    public void SetTradeOffer(
        string partyId,
        ItemRosterElementData[] offeredItems,
        TroopRosterElementData[] offeredTroops,
        int offeredGold,
        string[] offeredFiefs,
        TroopRosterElementData[] offeredPrisoners,
        bool offeredPeace)
    {
        if (partyId == InitiatorPartyId)
        {
            InitiatorOfferedItems = offeredItems ?? new ItemRosterElementData[0];
            InitiatorOfferedTroops = offeredTroops ?? new TroopRosterElementData[0];
            InitiatorOfferedGold = offeredGold;
            InitiatorOfferedFiefs = offeredFiefs ?? new string[0];
            InitiatorOfferedPrisoners = offeredPrisoners ?? new TroopRosterElementData[0];
            InitiatorOfferedPeace = offeredPeace;
            return;
        }

        if (partyId == ResponderPartyId)
        {
            ResponderOfferedItems = offeredItems ?? new ItemRosterElementData[0];
            ResponderOfferedTroops = offeredTroops ?? new TroopRosterElementData[0];
            ResponderOfferedGold = offeredGold;
            ResponderOfferedFiefs = offeredFiefs ?? new string[0];
            ResponderOfferedPrisoners = offeredPrisoners ?? new TroopRosterElementData[0];
            ResponderOfferedPeace = offeredPeace;
        }
    }
}
