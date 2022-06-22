using System;
using System.Reflection;
using System.Runtime.Serialization;
using Common;
using Coop.Mod.Data;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Persistence
{
    [Collection("UsesGlobalPatcher")]
    public class CoopRailScope_Test : IDisposable
    {
        private readonly TestEnvironment m_Environment = new TestEnvironment(
            1,
            Registry.Client,
            Registry.Server);
        
        public CoopRailScope_Test()
        {
            Persistence = m_Environment.Persistence ??
                          throw new Exception("Persistence may not be null. Error in test setup.");
        }
        
        public void Dispose()
        {
            m_Environment.Destroy();
        }
        
        private TestPersistence Persistence { get; }
        private const int ClientId0 = 0;

        [Fact]
        void MobilePartyIsSynced()
        {
            MobileParty party = CreateMobileParty();
            Guid guid = CoopObjectManager.AddObject(party);
            Persistence.Server.Room.AddNewEntity<MobilePartyEntityServer>(
                e => e.State.PartyId = guid);
            
            m_Environment.Persistence.UpdateServer();
            m_Environment.ExecuteSendsServer();
            m_Environment.Persistence.UpdateClients();
        }

        MobileParty CreateMobileParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }
    }
}