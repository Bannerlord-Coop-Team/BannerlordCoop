﻿using ProtoBuf;

namespace GameInterface.Services.Players.Data;

/// <summary>
/// Holds information about the players.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record Player
{
    public Player(byte[] heroData, string heroStringId, string partyStringId, string characterObjectStringId, string clanStringId)
    {
        HeroData = heroData;
        HeroStringId = heroStringId;
        PartyStringId = partyStringId;
        CharacterObjectStringId = characterObjectStringId;
        ClanStringId = clanStringId;
    }

    [ProtoMember(1)]
    public byte[] HeroData { get; }
    [ProtoMember(2)]
    public string HeroStringId { get; }
    [ProtoMember(3)]
    public string PartyStringId { get; }
    [ProtoMember(4)]
    public string CharacterObjectStringId { get; }
    [ProtoMember(5)]
    public string ClanStringId { get; }
}
