using Common.Logging;
using GameInterface.Services.MobileParties;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes
{
    internal interface IControlledHeroRegistry
    {
        ISet<Guid> ControlledHeros { get; }
        void RegisterExistingHeroes(IEnumerable<Guid> heroIds);
        bool IsControlled(Guid heroId);
        bool RegisterAsControlled(Guid heroId);
        bool RemoveAsControlled(Guid heroId);
    }

    internal class ControlledHeroRegistry : IControlledHeroRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ControlledHeroRegistry>();

        public ISet<Guid> ControlledHeros { get; } = new HashSet<Guid>();
        public void RegisterExistingHeroes(IEnumerable<Guid> heroIds)
        {
            var badIds = new List<string>();
            foreach(var heroId in heroIds)
            {
                if(RegisterAsControlled(heroId) == false)
                {
                    // Store bad id for logging
                    badIds.Add(heroId.ToString());
                }
            }

            if(badIds.IsEmpty() == false)
            {
                Logger.Error($"Could not register the following Hero ids " +
                    $"as controlled {badIds}");
            }
        }
        public bool RegisterAsControlled(Guid heroId) => ControlledHeros.Add(heroId);

        public bool IsControlled(Guid heroId) => ControlledHeros.Contains(heroId);

        public bool RemoveAsControlled(Guid heroId) => ControlledHeros.Remove(heroId);
    }
}
