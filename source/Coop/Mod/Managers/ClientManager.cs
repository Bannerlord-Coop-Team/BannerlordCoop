using System;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using JetBrains.Annotations;

namespace Coop.Mod.Managers
{
    public class ClientManager : CampaignGameManager
    {
        /// <summary>
        /// The clients hero as it was sent to the server. Note that the server may change some fields when introducing the hero to the campaign.
        /// </summary>
        [NotNull] private readonly Hero m_PlayerAsSerialized;

        /// <summary>
        /// The clients hero as it exists in the server side campaign.
        /// </summary>
        [CanBeNull] Hero m_PlayerInCampaign;
        public ClientManager(LoadResult saveGameData, Hero playerAsSerialized) : base(saveGameData) { m_PlayerAsSerialized = playerAsSerialized; }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnOnLoadFinishedEventHandler OnPreLoadFinishedEvent;
        public static event OnOnLoadFinishedEventHandler OnPostLoadFinishedEvent;
        public override void OnLoadFinished()
        {
            OnPreLoadFinishedEvent?.Invoke(this, EventArgs.Empty);
            base.OnLoadFinished();
            OnPostLoadFinishedEvent?.Invoke(this, EventArgs.Empty);

            MobileParty playerParty = MobileParty.All.AsParallel().SingleOrDefault(IsClientPlayersParty);
            if(playerParty != null)
            {
                m_PlayerInCampaign = playerParty.LeaderHero;

                // Switch current player party from host to client party
                ChangePlayerCharacterAction.Apply(m_PlayerInCampaign);

                // Start player at training field
                Settlement settlement = Settlement.Find("tutorial_training_field");
                Campaign.Current.HandleSettlementEncounter(MobileParty.MainParty, settlement);
                PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("training_field"), null, null, null);
            }
            else
            {
                // Might need to adjust IsClientPlayersParty
                throw new Exception("Transferred player party could not be found");
            }
        }

        public new void OnTick(float dt)
        {
            FieldInfo entityFieldInfo = typeof(GameManagerBase).GetField("_entitySystem", BindingFlags.Instance | BindingFlags.NonPublic);
            if(entityFieldInfo.GetValue(this) == null)
            {
                entityFieldInfo.SetValue(this, new EntitySystem<GameManagerComponent>());
            }
            base.OnTick(dt);
        }

        private bool IsClientPlayersParty(MobileParty candidate)
        {
            // This comparison is subject to change
            Hero candidateHero = candidate.LeaderHero;
            if (candidateHero == null)
            {
                return false;
            }

            // Hero itself is always sent
            if (candidateHero.Name.ToString() != m_PlayerAsSerialized.Name.ToString())
            {
                return false;
            }

            // Clan as well
            if (candidateHero.Clan.Name.ToString() != m_PlayerAsSerialized.Clan.Name.ToString())
            {
                return false;
            }

            // Parents are missing for now
            if (candidateHero.Father != null || candidateHero.Mother != null)
            {
                return false;
            }

            return true;
        }
    }
}
