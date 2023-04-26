using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Save.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class GameObjectGuids
    {
        [ProtoMember(1)]
        public string[] ControlledHeros { get; set; } = Array.Empty<string>();


        public GameObjectGuids(string[] controlledHeros)
        {
            ControlledHeros = controlledHeros;
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

            return true;
        }

        public override int GetHashCode() => base.GetHashCode();
    }
}
