using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes
{
    internal interface IControlledHeroRegistry
    {
        HashSet<Guid> ControlledHeros { get; }

        bool IsControlled(Guid heroId);
        bool RegisterAsControlled(Guid heroId);
        bool RemoveAsControlled(Guid heroId);
    }

    internal class ControlledHeroRegistry : IControlledHeroRegistry
    {
        public HashSet<Guid> ControlledHeros { get; } = new HashSet<Guid>();

        public bool RegisterAsControlled(Guid heroId) => ControlledHeros.Add(heroId);

        public bool IsControlled(Guid heroId) => ControlledHeros.Contains(heroId);

        public bool RemoveAsControlled(Guid heroId) => ControlledHeros.Remove(heroId);
    }
}
