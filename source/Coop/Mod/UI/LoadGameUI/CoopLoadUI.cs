using TaleWorlds.Core;
using SandBox.View;
using SandBox.ViewModelCollection.SaveLoad;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Missions;
using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using System.Linq;
using TaleWorlds.SaveSystem;

namespace Coop.Mod.UI
{
	class SelectedGameVM : SavedGameVM
	{
		private readonly Action<SavedGameVM> _onDelete;
		private readonly Action<SavedGameVM> _onSelection;
		public SelectedGameVM(SaveGameFileInfo save, bool isSaving, Action<SavedGameVM> onDelete, Action<SavedGameVM> onSelection, Action onCancelLoadSave, Action onDone) :
			base(save, isSaving, onDelete, onSelection, onCancelLoadSave, onDone)
		{
			_onDelete = onDelete;
			_onSelection = onSelection;
		}

		private new void ExecuteDelete()
		{
			this._onDelete(this);
		}

		private new void ExecuteSelection()
		{
			this._onSelection(this);
		}

		public new void ExecuteSaveLoad()
		{
			LoadGameResult saveGameData = MBSaveLoad.LoadSaveGameData(Save.Name, Utilities.GetModulesNames());
			if (saveGameData != null)
			{
				if (saveGameData.ModuleCheckResults.Count > 0)
				{
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=kJtNMYum}Module mismatch", null).ToString(), new TextObject("{=lh0so0uX}Do you want to load the saved game with different modules?", null).ToString(), true, true, new TextObject("{=aeouhelq}Yes", null).ToString(), new TextObject("{=8OkPHu4f}No", null).ToString(), delegate ()
					{
						StartGame(saveGameData.LoadResult);
					}, null, ""), false);
					return;
				}
				StartGame(saveGameData.LoadResult);
			}
		}

		private void StartGame(LoadResult loadResult)
		{
			if (Game.Current != null)
			{
				ScreenManager.PopScreen();
				GameStateManager.Current.CleanStates(0);
				GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
			}
            MBGameManager.StartNewGame(CoopServer.Instance.CreateGameManager(loadResult));
		}

	}

	class CoopLoadUI : SaveLoadVM
    {
		private new SelectedGameVM CurrentSelectedSave;
		public CoopLoadUI() : base(false)
		{
			GetSavedGames().Clear();
			SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles();
			for (int i = 0; i < saveFiles.Length; i++)
			{
				SelectedGameVM item = new SelectedGameVM(saveFiles[i], this.IsSaving, new Action<SavedGameVM>(this.OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(this.OnCancelLoadSave), new Action(ExecuteDone));
				GetSavedGames().Add(item);
			}
			OnSaveSelection(GetSavedGames().FirstOrDefault<SavedGameVM>());
			RefreshValues();
		}

		private void OnSaveSelection(SavedGameVM saveGame)
		{
			SelectedGameVM save = (SelectedGameVM)saveGame;
			if (save != CurrentSelectedSave)
			{
				if (CurrentSelectedSave != null)
				{
					CurrentSelectedSave.IsSelected = false;
				}
				CurrentSelectedSave = save;
				if (CurrentSelectedSave != null)
				{
					CurrentSelectedSave.IsSelected = true;
				}
				IsActionEnabled = (CurrentSelectedSave != null);
			}
		}

		private new void ExecuteLoadSave()
		{
			SavedGameVM currentSelectedSave = CurrentSelectedSave;
			if (currentSelectedSave == null)
			{
				return;
			}
			currentSelectedSave.ExecuteSaveLoad();
		}

		private new void ExecuteDone()
		{
			ScreenManager.PopScreen();
		}

		private void OnCancelLoadSave()
		{
		}

		private void OnDeleteSavedGame(SavedGameVM savedGame)
		{
			string titleText = new TextObject("{=QHV8aeEg}Delete Save", null).ToString();
			string text = new TextObject("{=HH2mZq8J}Are you sure you want to delete this save game?", null).ToString();
			InformationManager.ShowInquiry(new InquiryData(titleText, text, true, true, new TextObject("{=aeouhelq}Yes", null).ToString(), new TextObject("{=8OkPHu4f}No", null).ToString(), delegate ()
			{
				MBSaveLoad.DeleteSaveGame(savedGame.Save.Name);
				GetSavedGames().Remove(savedGame);
				OnSaveSelection(GetSavedGames().FirstOrDefault());
			}, null, ""), false);
		}

		private MBBindingList<SavedGameVM> GetSavedGames() => SaveGroups.FirstOrDefault().SavedGamesList;
		
	}

	class CoopLoadScreen : SaveLoadScreen
	{
		public CoopLoadScreen() : base(false)
		{
		}
	}

	[OverrideView(typeof(CoopLoadScreen))]
	public class CoopLoadGameGauntletScreen : ScreenBase
	{
		public CoopLoadGameGauntletScreen() { }

		protected override void OnInitialize()
		{
			base.OnInitialize();
			_datasource = new CoopLoadUI();
			_gauntletLayer = new GauntletLayer(1, "GauntletLayer");
			_gauntletLayer.LoadMovie("SaveLoadScreen", _datasource);
			_gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
			AddLayer(_gauntletLayer);
		}

		protected override void OnFinalize()
		{
			base.OnFinalize();
			RemoveLayer(this._gauntletLayer);
			_gauntletLayer = null;
			_datasource = null;
		}

		private GauntletLayer _gauntletLayer;

		private SaveLoadVM _datasource;
	}
	
}
