

using SandBox.View;
using SandBox.ViewModelCollection.SaveLoad;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Missions.Services.Arena.View
{

    class ArenaSelectedGameVM : SavedGameVM
	{
		private readonly Action<SavedGameVM> _onDelete;
		private readonly Action<SavedGameVM> _onSelection;
		public ArenaSelectedGameVM(SaveGameFileInfo save, bool isSaving, Action<SavedGameVM> onDelete, Action<SavedGameVM> onSelection, Action onCancelLoadSave, Action onDone) :
			base(save, isSaving, onDelete, onSelection, onCancelLoadSave, onDone)
		{
			_onDelete = onDelete;
			_onSelection = onSelection;
		}

		private new void ExecuteDelete()
		{
			_onDelete(this);
		}

		private new void ExecuteSelection()
		{
			_onSelection(this);
		}

		public new void ExecuteSaveLoad()
		{
			
		}

		private void StartGame(LoadResult loadResult)
		{
			if (Game.Current != null)
			{
				ScreenManager.PopScreen();
				GameStateManager.Current.CleanStates(0);
				GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
			}
		}

	}

	class ArenaLoadUI : SaveLoadVM
	{
		Action<SaveGameFileInfo> _loadAction;
		private static readonly HashSet<string> allowedModules = new HashSet<string>()
		{
			"Native",
			"Sandbox",
			"SandBox Core",
			"StoryMode",
			"CustomBattle",
			"BirthAndDeath",
			"MissionTestMod",
		};
		public ArenaLoadUI(Action<SaveGameFileInfo> loadAction) : base(false, false)
		{
			_loadAction = loadAction;
			IsVisualDisabled = false;
			
			MBBindingList<SavedGameGroupVM> newGroups = new MBBindingList<SavedGameGroupVM>();
			foreach (SavedGameGroupVM group in SaveGroups)
            {
				MBBindingList<SavedGameVM> newSaves = new MBBindingList<SavedGameVM>();
				
				foreach(SavedGameVM save in group.SavedGamesList)
                {
					bool containsCoop = false;
					bool containsNonApprovedMods = false;
					foreach(SavedGameModuleInfoVM savedGameModuleInfoVM in save.LoadedModulesInSave)
                    {
                        if (!allowedModules.Contains(savedGameModuleInfoVM.Definition))
                        {
							containsNonApprovedMods = true;
                        }
						if(!containsCoop && savedGameModuleInfoVM.Definition == "MissionTestMod")
                        {
							containsCoop = true;
                        }
                    }
					if(!containsNonApprovedMods && containsCoop)
                    {
						ArenaSelectedGameVM gameVm = new ArenaSelectedGameVM(save.Save, IsSaving, new Action<SavedGameVM>(OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(OnCancelLoadSave), new Action(ExecuteDone));
						newSaves.Add(gameVm);
                    }
					
                }
				if(newSaves.Count() <= 0)
                {
					continue;
                }
				newGroups.Add(group);
				group.SavedGamesList.Clear();
				group.SavedGamesList = newSaves;
            }
            SaveGroups = newGroups;
            OnSaveSelection(SaveGroups.FirstOrDefault((SavedGameGroupVM x) => x.SavedGamesList.Count > 0)?.SavedGamesList.FirstOrDefault());
			RefreshValues();
		}

		private void OnSaveSelection(SavedGameVM saveGame)
		{
			ArenaSelectedGameVM save = (ArenaSelectedGameVM)saveGame;
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

				IsAnyItemSelected = (CurrentSelectedSave != null);
				IsActionEnabled = (IsAnyItemSelected && !CurrentSelectedSave.IsCorrupted);
			}

		}

		private new void ExecuteLoadSave()
		{
			SavedGameVM currentSelectedSave = CurrentSelectedSave;
			if (currentSelectedSave == null)
			{
				return;
			}
			if (_loadAction != null) _loadAction.Invoke(currentSelectedSave.Save);
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
			}, null, ""), false);
		}



	}

	class ArenaLoadScreen : SaveLoadScreen
	{
		public ArenaLoadScreen() : base(false)
		{
		}
	}

	[OverrideView(typeof(ArenaLoadScreen))]
	public class ArenaLoadGameGauntletScreen : ScreenBase
	{
		public ArenaLoadGameGauntletScreen(Action<SaveGameFileInfo> loadAction) {
			_loadAction = loadAction;
		}

		protected override void OnInitialize()
		{
			base.OnInitialize();

			
			_datasource = new ArenaLoadUI(_loadAction);
			_gauntletLayer = new GauntletLayer(1, "GauntletLayer");
			_gauntletLayer.LoadMovie("SaveLoadScreen", _datasource);
			_gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
			SpriteData spriteData = UIResourceManager.SpriteData;
			TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
			ResourceDepot uiresourceDepot = UIResourceManager.UIResourceDepot;
			this._spriteCategory = spriteData.SpriteCategories["ui_saveload"];
			this._spriteCategory.Load(resourceContext, uiresourceDepot);
			this._gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
			this._gauntletLayer.IsFocusLayer = true;
			this._gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
			AddLayer(_gauntletLayer);
		}

		protected override void OnFinalize()
		{
			base.OnFinalize();
			RemoveLayer(_gauntletLayer);
			_gauntletLayer = null;
			_datasource = null;
			_spriteCategory = null;
		}

		private GauntletLayer _gauntletLayer;

		private SaveLoadVM _datasource;

		private SpriteCategory _spriteCategory;

		private Action<SaveGameFileInfo> _loadAction;
	}

}
