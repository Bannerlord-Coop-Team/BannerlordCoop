using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Messages;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
    }
    internal class RegistryInterface : IRegistryInterface
    {
        private readonly IMessageBroker messageBroker;
        private readonly IMobilePartyRegistry partyRegistry;
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;

        public RegistryInterface(
            IMessageBroker messageBroker,
            IMobilePartyRegistry partyRegistry,
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry) 
        {
            this.messageBroker = messageBroker;
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
    }
}
