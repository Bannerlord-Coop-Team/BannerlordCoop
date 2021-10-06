using System;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using JetBrains.Annotations;
using TaleWorlds.ObjectSystem;
using StoryMode;
using System.Diagnostics;

namespace Coop.Mod.Managers
{
    public class ClientManager : SandBoxGameManager
    {
        /// <summary>
        /// The clients hero as it was sent to the server. Note that the server may change some fields when introducing the hero to the campaign.
        /// </summary>
        [CanBeNull] private readonly Hero m_PlayerAsSerialized;
        [CanBeNull] private readonly MBGUID m_HeroGUID;

        /// <summary>
        /// The clients hero as it exists in the server side campaign.
        /// </summary>
        [CanBeNull] Hero m_PlayerInCampaign;
        public ClientManager(LoadResult saveGameData, Hero playerAsSerialized) : base(saveGameData) 
        { 
            m_PlayerAsSerialized = playerAsSerialized;
        }

        public ClientManager(LoadResult saveGameData,  MBGUID heroGUID) : base(saveGameData)
        {
            m_HeroGUID = heroGUID;
        }

        public static event EventHandler OnPreLoadFinishedEvent;
        public static event EventHandler OnPostLoadFinishedEvent;
        public override void OnLoadFinished()
        {
            OnPreLoadFinishedEvent?.Invoke(this, EventArgs.Empty);
            base.OnLoadFinished();
            OnPostLoadFinishedEvent?.Invoke(this, EventArgs.Empty);


            if (m_PlayerAsSerialized != null)
            {
                MobileParty playerParty = MobileParty.All.AsParallel().SingleOrDefault(IsClientPlayersParty);
                m_PlayerInCampaign = playerParty.LeaderHero;

                Debug.WriteLine($"{playerParty.Id}");

                // Start player at training field
                Settlement settlement = Settlement.Find("tutorial_training_field");

                ChangePlayerCharacterAction.Apply(m_PlayerInCampaign);

                EncounterManager.StartSettlementEncounter(playerParty, settlement);

                PlayerEncounter.EnterSettlement();
                PlayerEncounter.LocationEncounter = new TrainingFieldEncounter(settlement);
                LocationComplex complex = LocationComplex.Current;
                PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("training_field"), null, null, null);
            }
            else if(m_HeroGUID != null)
            {
                m_PlayerInCampaign = (Hero)MBObjectManager.Instance.GetObject(m_HeroGUID);

                // Switch current player party from host to client party
                ChangePlayerCharacterAction.Apply(m_PlayerInCampaign);
            }
            else
            {
                // Might need to adjust IsClientPlayersParty
                throw new Exception("Transferred player party could not be found");
            }

            // Switch current player party from host to client party
            ChangePlayerCharacterAction.Apply(m_PlayerInCampaign);
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
            if (candidateHero.FirstName.ToString() != m_PlayerAsSerialized.FirstName.ToString())
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
