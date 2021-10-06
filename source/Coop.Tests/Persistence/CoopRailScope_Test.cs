using System;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Moq;
using TaleWorlds.CampaignSystem;
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
            MobileParty party = (MobileParty) Activator.CreateInstance(typeof(MobileParty));
            Persistence.Server.Room.AddNewEntity<MobilePartyEntityServer>(
                e => e.State.PartyId = party.Id);
            
            m_Environment.Persistence.UpdateServer();
            m_Environment.ExecuteSendsServer();
            m_Environment.Persistence.UpdateClients();
        }
    }
}