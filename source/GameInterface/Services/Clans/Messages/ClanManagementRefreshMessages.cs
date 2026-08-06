using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshPartiesList : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshWorkshopsList : ICommand { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshClanMembersList : ICommand { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshAfterRoleAssignment : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public RefreshAfterRoleAssignment(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
