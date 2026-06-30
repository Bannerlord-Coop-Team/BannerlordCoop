using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.ObjectManager;

/// <summary>
/// A snapshot of the server's ids for non-MBObjectBase party attachments (PartyBase, member/prison
/// TroopRoster, ItemRoster, PartyComponent, MapEventSide) that were live-created on the server and so
/// carry a runtime "Created_N" counter id rather than the "{Type}_{owner.StringId}" a joining client
/// re-derives in RegisterAllObjects. Sent in the join save transfer; the joining client re-keys each
/// listed object from its re-derived id to the server's id (see AttachmentIdMapInitializationHandler)
/// so server-to-client AutoSync updates for that attachment resolve.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class AttachmentIdMap
{
    // Client-re-derived id ("{Type}_{owner.StringId}") -> the server's actual id for the same object.
    [ProtoMember(1)]
    public Dictionary<string, string> DerivedToServerId { get; }

    public AttachmentIdMap(Dictionary<string, string> derivedToServerId)
    {
        DerivedToServerId = derivedToServerId;
    }
}
