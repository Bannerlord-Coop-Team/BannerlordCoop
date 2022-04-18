using Coop.Mod.GameSync.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.DebugUtil
{
    public class PartySyncDebugBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.Tick));
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void Tick(float dt)
        {
            if(!m_DanceParams.HasValue)
            {
                return;
            }

            long time = CampaignTime.Now.GetNumTicks();
            long tLength = m_DanceParams.Value.animationLength.GetNumTicks();

            for(int i = 0; i < m_CreatedParties.Count; ++i)
            {
                MobileParty party = m_CreatedParties[i];
                long tOffset = (long)(tLength / m_CreatedParties.Count) * i;
                long tCurrent = (time + tOffset) % tLength;
                double t = (2 * Math.PI) / tLength * tCurrent;
                float x = m_DanceParams.Value.radius * (float)Math.Cos(t) + m_DanceParams.Value.center.x;
                float y = m_DanceParams.Value.radius * (float)Math.Sin(t) + m_DanceParams.Value.center.y;
                party.Position2D = new Vec2(x, y);
            }
        }

        public struct DanceParams
        {
            public Vec2 center;
            public float radius;
            public CampaignTime animationLength;
            public uint numberOfDancers;
        }
        
        public static void StartDancing(DanceParams p)
        {

            m_DanceParams = p;
            SpawnTestParties(p.center, p.numberOfDancers);
        }

        public static void StopDancing()
        {
            m_DanceParams = null;
            DespawnTestParties();
        }

        public static void AddToCounts(int i)
        {
            foreach(MobileParty p in m_CreatedParties)
            {
                TroopRosterElement first = p.MemberRoster.GetTroopRoster().First();
                p.MemberRoster.AddToCountsAtIndex(0, i);
            }
        }

        private static void SpawnTestParties(Vec2 position, uint numberOfParties)
        {
            for(int i = 0; i < numberOfParties; ++i)
            {
                m_CreatedParties.Add(PartySpawnHelper.SpawnTestersNearby(position, 5f));
            }
        }

        private static void DespawnTestParties()

        {
            foreach(MobileParty party in m_CreatedParties)
            {
                party.RemoveParty();
            }
            m_CreatedParties.Clear();
        }
        private static List<MobileParty> m_CreatedParties = new List<MobileParty> {};
        private static DanceParams? m_DanceParams;
    }
}
