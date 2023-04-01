using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Save.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Save.Interfaces
{
    internal interface IRegistryInterface : IGameAbstraction
    {
        Guid[] GetControlledHeroIds();
        IReadOnlyDictionary<string, Guid> GetHeroIds();
        IReadOnlyDictionary<string, Guid> GetPartyIds();
        void LoadObjectGuids(GameObjectGuids gameObjectGuids);
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

        public Guid[] GetControlledHeroIds() => controlledHeroRegistry.ControlledHeros.ToArray();

        public void LoadObjectGuids(GameObjectGuids gameObjectGuids)
        {
            controlledHeroRegistry.RegisterExistingHeroes(gameObjectGuids.ControlledHeros);
            heroRegistry.RegisterHeroesWithStringIds(gameObjectGuids.HeroIds);
            partyRegistry.RegisterPartiesWithStringIds(gameObjectGuids.PartyIds);
        }

        public void RegisterAllGameObjects()
        {
            partyRegistry.RegisterAllParties();
            heroRegistry.RegisterAllHeroes();
        }
    }
}
