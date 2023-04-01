using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Save.Interfaces
{
    internal interface IRegistryInterface : IGameAbstraction
    {
        ISet<Guid> GetControlledHeroIds();
        IReadOnlyDictionary<string, Guid> GetHeroIds();
        IReadOnlyDictionary<string, Guid> GetPartyIds();
        void LoadObjectGuids(
            ISet<Guid> controlledHeros,
            IReadOnlyDictionary<string, Guid> heroIds,
            IReadOnlyDictionary<string, Guid> partyIds);
        void RegisterAllGameObjects();
    }
    internal class RegistryInterface : IRegistryInterface
    {
        private readonly IMobilePartyRegistry partyRegistry;
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;

        public RegistryInterface(
            IMobilePartyRegistry partyRegistry,
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry) 
        {
            this.partyRegistry = partyRegistry;
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroRegistry;
        }

        public IReadOnlyDictionary<string, Guid> GetPartyIds()
        {
            return partyRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);
        }

        public IReadOnlyDictionary<string, Guid> GetHeroIds()
        {
            return heroRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);
        }

        public ISet<Guid> GetControlledHeroIds() => controlledHeroRegistry.ControlledHeros;

        public void LoadObjectGuids(
            ISet<Guid> controlledHeros, 
            IReadOnlyDictionary<string, Guid> heroIds,
            IReadOnlyDictionary<string, Guid> partyIds)
        {
            controlledHeroRegistry.RegisterExistingHeroes(controlledHeros);
            heroRegistry.RegisterHeroesWithStringIds(heroIds);
            partyRegistry.RegisterPartiesWithStringIds(partyIds);
        }

        public void RegisterAllGameObjects()
        {
            partyRegistry.RegisterAllParties();
            heroRegistry.RegisterAllHeroes();
        }
    }
}
