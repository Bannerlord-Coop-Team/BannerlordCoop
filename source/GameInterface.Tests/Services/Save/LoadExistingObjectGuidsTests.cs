using Autofac;
using Common.Messaging;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Xunit;
using GameInterface.Tests.Bootstrap.Extensions;

namespace GameInterface.Tests.Services.Save
{
    public class LoadExistingObjectGuidsTests
    {
        readonly IContainer _container;
        readonly IMessageBroker _messageBroker;
        public LoadExistingObjectGuidsTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();
        }

        [Fact]
        public void LoadExistingObjectGuids_SendReceive()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            var partyGuids = new (MobileParty, Guid)[NUM_PARTIES];
            var partyAssociations = new Dictionary<string, Guid>();

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                party.StringId = $"Party {i}";

                Campaign.Current.CampaignObjectManager.AddMobileParty(party);

                var partyId = Guid.NewGuid();

                partyGuids[i] = (party, partyId);
                partyAssociations.Add(party.StringId, partyId);
            }
        }
    }
}
