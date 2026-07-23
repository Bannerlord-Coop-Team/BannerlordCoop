using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddWarParty : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string WarPartyComponentId;

    public NetworkAddWarParty(string clanId, string warPartyComponentId)
    {
        ClanId = clanId;
        WarPartyComponentId = warPartyComponentId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveWarParty : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string WarPartyComponentId;

    public NetworkRemoveWarParty(string clanId, string warPartyComponentId)
    {
        ClanId = clanId;
        WarPartyComponentId = warPartyComponentId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddSupporterNotable : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkAddSupporterNotable(string clanId, string heroId)
    {
        ClanId = clanId;
        HeroId = heroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveSupporterNotable : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkRemoveSupporterNotable(string clanId, string heroId)
    {
        ClanId = clanId;
        HeroId = heroId;
    }
}