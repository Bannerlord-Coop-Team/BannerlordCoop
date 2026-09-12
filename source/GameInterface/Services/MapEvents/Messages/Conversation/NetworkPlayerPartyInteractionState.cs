using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyInteractionState : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly string OtherPartyId;
    [ProtoMember(4)]
    public readonly string OtherPlayerName;
    [ProtoMember(5)]
    public readonly PlayerPartyInteractionPhase Phase;
    [ProtoMember(6)]
    public readonly PlayerPartyInteractionProposal Proposal;
    [ProtoMember(7)]
    public readonly PlayerPartyInteractionOption[] Options;
    [ProtoMember(8)]
    public readonly bool InitiatorAcceptedTrade;
    [ProtoMember(9)]
    public readonly bool ResponderAcceptedTrade;
    [ProtoMember(10)]
    public readonly bool IsInitiator;
    [ProtoMember(11)]
    public readonly ItemRosterElementData[] PartyItems;
    [ProtoMember(12)]
    public readonly ItemRosterElementData[] OtherPartyItems;
    [ProtoMember(13)]
    public readonly PlayerPartyInteractionOption[] EnabledOptions;
    [ProtoMember(14)]
    public readonly bool IsHostile;
    [ProtoMember(15)]
    public readonly PlayerPartyInteractionVassalUnavailableReason VassalUnavailableReason;

    public NetworkPlayerPartyInteractionState(
        string sessionId,
        string partyId,
        string otherPartyId,
        string otherPlayerName,
        PlayerPartyInteractionPhase phase,
        PlayerPartyInteractionProposal proposal,
        PlayerPartyInteractionOption[] options,
        bool isInitiator,
        bool initiatorAcceptedTrade = false,
        bool responderAcceptedTrade = false,
        ItemRosterElementData[] partyItems = null,
        ItemRosterElementData[] otherPartyItems = null,
        PlayerPartyInteractionOption[] enabledOptions = null,
        bool isHostile = false,
        PlayerPartyInteractionVassalUnavailableReason vassalUnavailableReason = PlayerPartyInteractionVassalUnavailableReason.None)
    {
        SessionId = sessionId;
        PartyId = partyId;
        OtherPartyId = otherPartyId;
        OtherPlayerName = otherPlayerName;
        Phase = phase;
        Proposal = proposal;
        Options = options ?? new PlayerPartyInteractionOption[0];
        IsInitiator = isInitiator;
        InitiatorAcceptedTrade = initiatorAcceptedTrade;
        ResponderAcceptedTrade = responderAcceptedTrade;
        PartyItems = partyItems ?? new ItemRosterElementData[0];
        OtherPartyItems = otherPartyItems ?? new ItemRosterElementData[0];
        EnabledOptions = enabledOptions ?? Options;
        IsHostile = isHostile;
        VassalUnavailableReason = vassalUnavailableReason;
    }
}
