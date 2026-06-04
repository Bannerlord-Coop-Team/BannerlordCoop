using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct TroopRosterElementSurrogate
{
    [ProtoMember(1)]
    public string CharacterObjectId { get; set; }

    [ProtoMember(2)]
    public int Number { get; set; }

    [ProtoMember(3)]
    public int WoundedNumber { get; set; }

    [ProtoMember(4)]
    public int Xp { get; set; }

    public TroopRosterElementSurrogate(TroopRosterElement troopRosterElement)
    {
        if (troopRosterElement.Equals(new TroopRosterElement(null)))
        {
            CharacterObjectId = null;
            Number = 0;
            WoundedNumber = 0;
            Xp = 0;
            return;
        }

        CharacterObjectId = troopRosterElement.Character?.StringId;
        Number = troopRosterElement.Number;
        WoundedNumber = troopRosterElement.WoundedNumber;
        Xp = troopRosterElement.Xp;
    }

    public static implicit operator TroopRosterElementSurrogate(TroopRosterElement troopRosterElement)
    {
        return new TroopRosterElementSurrogate(troopRosterElement);
    }

    public static implicit operator TroopRosterElement(TroopRosterElementSurrogate surrogate)
    {
        var character = string.IsNullOrEmpty(surrogate.CharacterObjectId)
            ? null
            : MBObjectManager.Instance.GetObject<CharacterObject>(surrogate.CharacterObjectId);

        var troopRosterElement = new TroopRosterElement(character)
        {
            _number = surrogate.Number,
            _woundedNumber = surrogate.WoundedNumber,
            _xp = surrogate.Xp
        };

        return troopRosterElement;
    }
}

