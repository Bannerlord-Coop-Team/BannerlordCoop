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
        HashSet<Guid> GetControlledHeroIds();
        Dictionary<string, Guid> GetHeroIds();
        Dictionary<string, Guid> GetPartyIds();
        void LoadObjectGuids(
            IReadOnlyCollection<Guid> controlledHeros,
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

        public Dictionary<string, Guid> GetPartyIds()
        {
            return partyRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);
        }

        public Dictionary<string, Guid> GetHeroIds()
        {
            return heroRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);
        }

        public HashSet<Guid> GetControlledHeroIds() => controlledHeroRegistry.ControlledHeros;

        public void LoadObjectGuids(
            IReadOnlyCollection<Guid> controlledHeros, 
            IReadOnlyDictionary<string, Guid> heroIds,
            IReadOnlyDictionary<string, Guid> partyIds)
        {
            messageBroker.Publish(this, new RegisterExistingControlledHeroes(Guid.Empty, controlledHeros));
            messageBroker.Publish(this, new RegisterHeroesWithStringIds(Guid.Empty, heroIds));
            messageBroker.Publish(this, new RegisterPartiesWithStringIds(Guid.Empty, partyIds));
        }
    }
}
