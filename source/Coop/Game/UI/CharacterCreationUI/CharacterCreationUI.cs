using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBMultiplayerCampaign.Client.UI.CharacterCreation
{
	using TaleWorlds.Core;
	using SandBox.View;
	using SandBox.ViewModelCollection.SaveLoad;
	using TaleWorlds.Engine.Screens;
	using TaleWorlds.Engine.GauntletUI;
	using TaleWorlds.GauntletUI.Data;
	using TaleWorlds.Library;
	using TaleWorlds.MountAndBlade.View.Missions;
	using System;
	using TaleWorlds.MountAndBlade;
	using TaleWorlds.SaveSystem.Load;
	using TaleWorlds.Engine;
	using TaleWorlds.Localization;
	using System.Linq;
    using TaleWorlds.MountAndBlade.View.Screen;
    using TaleWorlds.CampaignSystem;
	using TaleWorlds.CampaignSystem.ViewModelCollection;
    using TaleWorlds.TwoDimension;
    using TaleWorlds.InputSystem;
    using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
    using TaleWorlds.Core.ViewModelCollection;
    using StoryMode.ViewModelCollection.CharacterCreationSystem;
    using StoryMode.CharacterCreationSystem;
    using StoryMode.View.CharacterCreationSystem;
    using StoryMode.GauntletUI.CharacterCreationSystem;

    namespace MBMultiplayerCampaign
	{

		class ClientCharacterCreationReviewStageVM : CharacterCreationReviewStageVM
		{
			public ClientCharacterCreationReviewStageVM(
				CharacterCreation characterCreation, 
				Action affirmativeAction, 
				TextObject affirmativeActionText, 
				Action negativeAction, 
				TextObject negativeActionText, 
				int currentStageIndex, 
				int totalStagesCount, 
				int furthestIndex, 
				Action<int> goToIndex) 
				: base(characterCreation, 
					  affirmativeAction, 
					  affirmativeActionText, 
					  negativeAction, 
					  negativeActionText, 
					  currentStageIndex, 
					  totalStagesCount, 
					  furthestIndex, 
					  goToIndex)
			{ }
		}

		class ClientCharacterCreationState : CharacterCreationState
		{
		}

		[GameStateScreen(typeof(ClientCharacterCreationState))]
		public class ClientCharacterCreationReviewStageView : CharacterCreationReviewStageView
		{
			public ClientCharacterCreationReviewStageView(
				CharacterCreation characterCreation, 
				ControlCharacterCreationStage affirmativeAction, 
				TextObject affirmativeActionText, 
				ControlCharacterCreationStage negativeAction, 
				TextObject negativeActionText, 
				ControlCharacterCreationStage onRefresh, 
				ControlCharacterCreationStageReturnInt getCurrentStageIndexAction, 
				ControlCharacterCreationStageReturnInt getTotalStageCountAction, 
				ControlCharacterCreationStageReturnInt getFurthestIndexAction, 
				ControlCharacterCreationStageWithInt goToIndexAction)
				: base(characterCreation, 
					  affirmativeAction, 
					  affirmativeActionText, 
					  negativeAction, 
					  negativeActionText, 
					  onRefresh, 
					  getCurrentStageIndexAction, 
					  getTotalStageCountAction, 
					  getFurthestIndexAction, 
					  goToIndexAction)
			{ }
			public override IEnumerable<ScreenLayer> GetLayers()
			{
				throw new NotImplementedException();
			}

			public override int GetVirtualStageCount()
			{
				throw new NotImplementedException();
			}

			public override void NextStage()
			{
				throw new NotImplementedException();
			}

			public override void PreviousStage()
			{
				throw new NotImplementedException();
			}
		}

	}

}
