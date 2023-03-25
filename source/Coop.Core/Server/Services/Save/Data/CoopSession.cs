using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Save.Data
{
    public interface ICoopSession
    {
        string UniqueGameId { get; set; }
        Guid SessionId { get; set; }
        HashSet<Guid> ControlledHeroes { get; set; }
        Dictionary<string, Guid> HeroStringIdToGuid { get; set; }
        Dictionary<string, Guid> PartyStringIdToGuid { get; set; }
    }

    public class CoopSession : ICoopSession
    {
        public string UniqueGameId { get; set; }
        public Guid SessionId { get; set; }
        public HashSet<Guid> ControlledHeroes { get; set; }
        public Dictionary<string, Guid> HeroStringIdToGuid { get; set; }
        public Dictionary<string, Guid> PartyStringIdToGuid { get; set; }
    }
}
