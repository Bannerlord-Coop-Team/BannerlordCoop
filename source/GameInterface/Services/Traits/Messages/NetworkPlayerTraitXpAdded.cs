using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Traits.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerTraitXpAdded : IEvent
{
    [ProtoMember(1)]
    public readonly uint HeroHandle;

    [ProtoMember(2)]
    public readonly uint TraitHandle;

    [ProtoMember(3)]
    public readonly int PreviousLevel;

    [ProtoMember(4)]
    public readonly bool LevelChanged;

    [ProtoMember(5)]
    public readonly bool AddLogEntry;

    [ProtoMember(6)]
    public readonly ActionNotes Note;

    [ProtoMember(7)]
    public readonly uint ReferenceHeroHandle;

    public NetworkPlayerTraitXpAdded(
        uint heroHandle,
        uint traitHandle,
        int previousLevel,
        bool levelChanged,
        bool addLogEntry,
        ActionNotes note,
        uint referenceHeroHandle)
    {
        HeroHandle = heroHandle;
        TraitHandle = traitHandle;
        PreviousLevel = previousLevel;
        LevelChanged = levelChanged;
        AddLogEntry = addLogEntry;
        Note = note;
        ReferenceHeroHandle = referenceHeroHandle;
    }
}
