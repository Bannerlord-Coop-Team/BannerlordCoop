using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Data
{
    public interface ICoopSession
    {
        string UniqueGameId { get; set; }
        ISet<Guid> ControlledHeroes { get; set; }
        IReadOnlyDictionary<string, Guid> HeroStringIdToGuid { get; set; }
        IReadOnlyDictionary<string, Guid> PartyStringIdToGuid { get; set; }
    }

    [ProtoContract]
    public class CoopSession : ICoopSession
    {
        [ProtoMember(1)]
        public string UniqueGameId { get; set; }
        [ProtoMember(2)]
        public ISet<Guid> ControlledHeroes { get; set; }
        [ProtoMember(3)]
        public IReadOnlyDictionary<string, Guid> HeroStringIdToGuid { get; set; }
        [ProtoMember(4)]
        public IReadOnlyDictionary<string, Guid> PartyStringIdToGuid { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is CoopSession session == false) return false;

            if (UniqueGameId != session.UniqueGameId) return false;

            if (ControlledHeroes != null && session.ControlledHeroes != null)
            {
                if (ControlledHeroes.SetEquals(session.ControlledHeroes) == false) return false;
            }
            else
            {
                if(ControlledHeroes != session.ControlledHeroes) return false;
            }

            if (HeroStringIdToGuid != null && session.HeroStringIdToGuid != null)
            {
                if (HeroStringIdToGuid.SequenceEqual(session.HeroStringIdToGuid) == false) return false;
            }
            else
            {
                if (HeroStringIdToGuid != session.HeroStringIdToGuid) return false;
            }

            if (PartyStringIdToGuid != null && session.PartyStringIdToGuid != null)
            {
                if (PartyStringIdToGuid.SequenceEqual(session.PartyStringIdToGuid) == false) return false;
            }
            else
            {
                if (PartyStringIdToGuid != session.PartyStringIdToGuid) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
