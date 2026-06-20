using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct AddWarParty : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string WarPartyComponentId;

    public AddWarParty(string clanId, string warPartyComponentId)
    {
        ClanId = clanId;
        WarPartyComponentId = warPartyComponentId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemoveWarParty : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string WarPartyComponentId;

    public RemoveWarParty(string clanId, string warPartyComponentId)
    {
        ClanId = clanId;
        WarPartyComponentId = warPartyComponentId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct AddSupporterNotable : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public AddSupporterNotable(string clanId, string heroId)
    {
        ClanId = clanId;
        HeroId = heroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemoveSupporterNotable : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public RemoveSupporterNotable(string clanId, string heroId)
    {
        ClanId = clanId;
        HeroId = heroId;
    }
}