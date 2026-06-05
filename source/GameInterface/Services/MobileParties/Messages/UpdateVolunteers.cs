using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdateVolunteers : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, string[]> UpdatedVolunteerTypeIds;

    public UpdateVolunteers(Dictionary<string, string[]> updatedVolunteerTypeIds)
    {
        UpdatedVolunteerTypeIds = updatedVolunteerTypeIds;
    }
}