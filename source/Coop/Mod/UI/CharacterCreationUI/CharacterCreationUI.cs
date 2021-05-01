using System;
using System.Collections.Generic;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View.Screen;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using SandBox.GauntletUI.CharacterCreation;

namespace Coop.Client.UI.CharacterCreationVM
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
		/*
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
		*/
	}

}
