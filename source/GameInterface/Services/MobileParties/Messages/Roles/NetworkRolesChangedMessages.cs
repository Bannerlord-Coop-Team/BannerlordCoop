using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Messages.Roles;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemoveAllPartyRolesOfHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public RemoveAllPartyRolesOfHero(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemovePartyRoleOfHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    [ProtoMember(3)]
    public readonly PartyRole PartyRole;

    public RemovePartyRoleOfHero(string heroId, string mobilePartyId, PartyRole partyRole)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        PartyRole = partyRole;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SetPartyScout : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public SetPartyScout(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SetPartyQuartermaster : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public SetPartyQuartermaster(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SetPartyEngineer : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public SetPartyEngineer(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SetPartySurgeon : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public SetPartySurgeon(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}