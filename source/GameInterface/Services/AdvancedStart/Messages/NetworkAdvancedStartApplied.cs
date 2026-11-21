using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.AdvancedStart.Messages;

/// <summary>
/// Server to clients: a joining player's starting option was applied. Carries the hero's battle, civilian and
/// stealth equipment, because the start writes equipment slots directly and those writes don't replicate.
/// Stopgap until equipment sync (#4786) lands; remove with <see cref="Handlers.AdvancedStartHandler"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkAdvancedStartApplied : IEvent
{
    [ProtoMember(1)]
    public string HeroId { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }
    [ProtoMember(3)]
    public EquipmentData[] Equipment { get; }

    public NetworkAdvancedStartApplied(string heroId, string mobilePartyId, EquipmentData[] equipment)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        Equipment = equipment;
    }
}
