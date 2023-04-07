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
        ISet<string> ControlledHeros { get; }
        void RegisterExistingHeroes(IEnumerable<string> heroIds);
        bool IsControlled(string heroId);
        bool RegisterAsControlled(string heroId);
        bool RemoveAsControlled(string heroId);
    }

    internal class ControlledHeroRegistry : IControlledHeroRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ControlledHeroRegistry>();

        public ISet<string> ControlledHeros { get; } = new HashSet<string>();
        public void RegisterExistingHeroes(IEnumerable<string> heroIds)
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
        public bool RegisterAsControlled(string heroId) => ControlledHeros.Add(heroId);

        public bool IsControlled(string heroId) => ControlledHeros.Contains(heroId);

        public bool RemoveAsControlled(string heroId) => ControlledHeros.Remove(heroId);
    }
}
