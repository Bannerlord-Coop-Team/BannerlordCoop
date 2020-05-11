using System.Linq;
using Coop.Game.Patch;
using Coop.Game.Persistence;
using Coop.Sync;
using HarmonyLib;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        #region MobileParty
        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }
        #endregion

        #region TimeControl
        [CanBeNull] private RemoteValue<CampaignTimeControlMode> m_TimeControlMode;

        public RemoteValue<CampaignTimeControlMode> TimeControlMode
        {
            get => m_TimeControlMode;
            set
            {
                if (m_TimeControlMode != null)
                {
                    m_TimeControlMode.OnValueChanged -=
                        TimeControl.SetForced_Campaign_TimeControlMode;
                }

                m_TimeControlMode = value;
                if (m_TimeControlMode != null)
                {
                    m_TimeControlMode.OnValueChanged +=
                        TimeControl.SetForced_Campaign_TimeControlMode;
                }
            }
        }

        public Field TargetPosition { get; } =
            new Field(AccessTools.Field(typeof(MobileParty), "_targetPosition"));
        #endregion
    }
}
