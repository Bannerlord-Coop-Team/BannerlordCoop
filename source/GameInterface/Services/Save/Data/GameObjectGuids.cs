using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Save.Data
{
    [ProtoContract]
    public class GameObjectGuids
    {
        [ProtoMember(1)]
        public Guid[] ControlledHeros { get; set; }
        [ProtoMember(2)]
        public IReadOnlyDictionary<string, Guid> PartyIds { get; set; }
        [ProtoMember(3)]
        public IReadOnlyDictionary<string, Guid> HeroIds { get; set; }

        public GameObjectGuids()
        {
            ControlledHeros = Array.Empty<Guid>();
            PartyIds = new Dictionary<string, Guid>();
            HeroIds = new Dictionary<string, Guid>();
        }

        public GameObjectGuids(
            Guid[] controlledHeros,
            IReadOnlyDictionary<string, Guid> partyIds,
            IReadOnlyDictionary<string, Guid> heroIds)
        {
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }

        public override bool Equals(object obj)
        {
            if (obj is GameObjectGuids otherObjectGuids == false) return false;

            if (ControlledHeros != null && otherObjectGuids.ControlledHeros != null)
            {
                if (ControlledHeros.SequenceEqual(otherObjectGuids.ControlledHeros) == false) return false;
            }
            else
            {
                if (ControlledHeros != otherObjectGuids.ControlledHeros) return false;
            }

            if (PartyIds != null && otherObjectGuids.PartyIds != null)
            {
                if (PartyIds.SequenceEqual(otherObjectGuids.PartyIds) == false) return false;
            }
            else
            {
                if (PartyIds != otherObjectGuids.PartyIds) return false;
            }

            if (HeroIds != null && otherObjectGuids.HeroIds != null)
            {
                if (HeroIds.SequenceEqual(otherObjectGuids.HeroIds) == false) return false;
            }
            else
            {
                if (HeroIds != otherObjectGuids.HeroIds) return false;
            }

            return true;
        }

        public override int GetHashCode() => base.GetHashCode();
    }
}
