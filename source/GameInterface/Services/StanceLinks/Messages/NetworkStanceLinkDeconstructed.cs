using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.StanceLinks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkStanceLinkDeconstructed : ICommand
{
    [ProtoMember(1)]
    public readonly string Faction1Id;
    [ProtoMember(2)]
    public readonly string[] RemovedStanceLinkIds;

    public NetworkStanceLinkDeconstructed(string faction1Id, string[] removedStanceLinkIds)
    {
        Faction1Id = faction1Id;
        RemovedStanceLinkIds = removedStanceLinkIds;
    }
}