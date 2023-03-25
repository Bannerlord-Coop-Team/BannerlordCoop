using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Save.Data
{
    public interface ICoopSession
    {
        string UniqueGameId { get; set; }
        HashSet<Guid> ControlledHeroes { get; set; }
        Dictionary<string, Guid> HeroStringIdToGuid { get; set; }
        Dictionary<string, Guid> PartyStringIdToGuid { get; set; }
    }

    [ProtoContract]
    public class CoopSession : ICoopSession
    {
        [ProtoMember(1)]
        public string UniqueGameId { get; set; }
        [ProtoMember(2)]
        public HashSet<Guid> ControlledHeroes { get; set; }
        [ProtoMember(3)]
        public Dictionary<string, Guid> HeroStringIdToGuid { get; set; }
        [ProtoMember(4)]
        public Dictionary<string, Guid> PartyStringIdToGuid { get; set; }
    }
}
