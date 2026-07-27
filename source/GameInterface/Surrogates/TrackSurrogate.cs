using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Surrogates;

[ProtoContract]
internal class TrackSurrogate
{
    [ProtoMember(1)]
    public CampaignVec2 Position { get; set; }

    [ProtoMember(2)]
    public float Direction { get; set; }

    [ProtoMember(3)]
    public string PartyName { get; set; }

    [ProtoMember(4)]
    public string CultureId { get; set; }

    [ProtoMember(5)]
    public float Speed { get; set; }

    [ProtoMember(6)]
    public int NumberOfAllMembers { get; set; }

    [ProtoMember(7)]
    public int NumberOfMenWithHorse { get; set; }

    [ProtoMember(8)]
    public int NumberOfMenWithoutHorse { get; set; }

    [ProtoMember(9)]
    public int NumberOfPackAnimals { get; set; }

    [ProtoMember(10)]
    public int NumberOfPrisoners { get; set; }

    [ProtoMember(11)]
    public CampaignTime CreationTime { get; set; }

    [ProtoMember(12)]
    public float Life { get; set; }

    [ProtoMember(13)]
    public Track.PartyTypeEnum PartyType { get; set; }

    public static implicit operator TrackSurrogate(Track track)
    {
        if (track is null)
            return null!;

        return new TrackSurrogate
        {
            Position = track.Position,
            Direction = track.Direction,
            PartyName = track.PartyName.ToString(),
            CultureId = track.Culture?.StringId,
            Speed = track.Speed,
            NumberOfAllMembers = track.NumberOfAllMembers,
            NumberOfMenWithHorse = track.NumberOfMenWithHorse,
            NumberOfMenWithoutHorse = track.NumberOfMenWithoutHorse,
            NumberOfPackAnimals = track.NumberOfPackAnimals,
            NumberOfPrisoners = track.NumberOfPrisoners,
            CreationTime = track.CreationTime,
            Life = track.Life,
            PartyType = track.PartyType
        };
    }

    public static implicit operator Track(TrackSurrogate surrogate)
    {
        var culture = string.IsNullOrEmpty(surrogate.CultureId)
            ? null
            : MBObjectManager.Instance.GetObject<CultureObject>(surrogate.CultureId);

        var track = new Track
        {
            Position = surrogate.Position,
            Direction = surrogate.Direction,
            PartyName = new TextObject(surrogate.PartyName),
            Culture = culture,
            Speed = surrogate.Speed,
            NumberOfAllMembers = surrogate.NumberOfAllMembers,
            NumberOfMenWithHorse = surrogate.NumberOfMenWithHorse,
            NumberOfMenWithoutHorse = surrogate.NumberOfMenWithoutHorse,
            NumberOfPackAnimals = surrogate.NumberOfPackAnimals,
            NumberOfPrisoners = surrogate.NumberOfPrisoners,
            CreationTime = surrogate.CreationTime,
            Life = surrogate.Life,
            PartyType = surrogate.PartyType
        };

        return track;
    }
}

