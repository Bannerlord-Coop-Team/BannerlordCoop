using TaleWorlds.Core;

namespace GameInterface.Tests.Stubs
{
    public class GameManagerStub : GameManagerBase
    {
        public override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            throw new System.NotImplementedException();
        }

        public override void BeginGameStart(Game game)
        {
            throw new System.NotImplementedException();
        }

        public override void OnNewCampaignStart(Game game, object starterObject)
        {
            throw new System.NotImplementedException();
        }

        public override void OnAfterCampaignStart(Game game)
        {
            throw new System.NotImplementedException();
        }

        public override void RegisterSubModuleObjects(bool isSavedCampaign)
        {
            throw new System.NotImplementedException();
        }

        public override void AfterRegisterSubModuleObjects(bool isSavedCampaign)
        {
            throw new System.NotImplementedException();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            throw new System.NotImplementedException();
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            throw new System.NotImplementedException();
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            throw new System.NotImplementedException();
        }

        public override void OnAfterGameInitializationFinished(Game game, object initializerObject)
        {
            throw new System.NotImplementedException();
        }

        public override float ApplicationTime { get; }
        public override bool CheatMode { get; }
        public override bool IsDevelopmentMode { get; }
        public override bool IsEditModeOn { get; }
        public override bool DeterministicMode { get; }
        public override UnitSpawnPrioritizations UnitSpawnPrioritization { get; }
    }
}