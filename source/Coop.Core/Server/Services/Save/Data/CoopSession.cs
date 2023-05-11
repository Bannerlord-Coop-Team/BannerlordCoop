using GameInterface.Services.Heroes.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Data
{
    public interface ICoopSession
    {
        string UniqueGameId { get; set; }
        GameObjectGuids GameObjectGuids { get; set; }
    }

    [ProtoContract]
    public class CoopSession : ICoopSession
    {
        [ProtoMember(1)]
        public string UniqueGameId { get; set; }
        [ProtoMember(2)]
        public GameObjectGuids GameObjectGuids { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is CoopSession session == false) return false;

            if (UniqueGameId != session.UniqueGameId) return false;

            if (GameObjectGuids.Equals(session.GameObjectGuids) == false) return false;

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
