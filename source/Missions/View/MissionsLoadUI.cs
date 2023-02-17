

using SandBox.View;
using SandBox.ViewModelCollection.SaveLoad;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Missions.View
{
	/// <summary>
	/// VM for the game selection for missions
	/// </summary>
    class MissionSelectedGameVM : SavedGameVM
	{
		/// <summary>
		/// Class instance for when the game is deleted
		/// </summary>
		private readonly Action<SavedGameVM> _onDelete;

		/// <summary>
		/// Class instance for when the game is selected
		/// </summary>
		private readonly Action<SavedGameVM> _onSelection;

		/// <summary>
		/// Selected save game VM for Missions
		/// </summary>
		/// <param name="save">Information of the game save</param>
		/// <param name="isSaving">True if the user selected to save. Should always be false here</param>
		/// <param name="onDelete">Callback when the save game is deleted</param>
		/// <param name="onSelection">Callback when the save game is selected</param>
		/// <param name="onCancelLoadSave">Callback when the save load is cancelled</param>
		/// <param name="onDone">Callback when finished</param>
		public MissionSelectedGameVM(SaveGameFileInfo save, bool isSaving, Action<SavedGameVM> onDelete, Action<SavedGameVM> onSelection, Action onCancelLoadSave, Action onDone) :
			base(save, isSaving, onDelete, onSelection, onCancelLoadSave, onDone)
		{
			_onDelete = onDelete;
			_onSelection = onSelection;
		}

		/// <summary>
		/// Callback when the save is deleted. Passed in from the MissionLoadVM
		/// </summary>
		private new void ExecuteDelete()
		{
			_onDelete(this);
		}

		/// <summary>
		/// Callback when the save is selected. Passed in from the MissionLoadVM
		/// </summary>
		private new void ExecuteSelection()
		{
			_onSelection(this);
		}

		/// <summary>
		/// Callback when the save is loaded
		/// </summary>
		public new void ExecuteSaveLoad()
		{
			
		}

		/// <summary>
		/// Callback when game is started
		/// </summary>
		/// <param name="loadResult"></param>
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
	/// <summary>
	/// MissionLoadVM extends SaveLoadVM
	/// </summary>
	class MissionLoadVM : SaveLoadVM
	{
		/// <summary>
		/// Class instance for save game load callback
		/// </summary>
		Action<SaveGameFileInfo> _loadAction;

		/// <summary>
		/// Hard coded list of approved modules
		/// </summary>
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

		/// <summary>
		/// Loop through all save games
		/// Determine is they are valid save games
		/// They are valid save games if they contain the Coop Module AND do not 
		/// contain any non-approved modules
		/// </summary>
		/// <param name="loadAction">Callback when the user selects to load the game</param>
		public MissionLoadVM(Action<SaveGameFileInfo> loadAction) : base(false, false)
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
						MissionSelectedGameVM gameVm = new MissionSelectedGameVM(save.Save, IsSaving, new Action<SavedGameVM>(OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(OnCancelLoadSave), new Action(ExecuteDone));
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

		/// <summary>
		/// When a save is clicked on, this callback gets triggered
		/// Sets the save selection in view
		/// </summary>
		/// <param name="saveGame">Click on save game</param>
		private void OnSaveSelection(SavedGameVM saveGame)
		{
			MissionSelectedGameVM save = (MissionSelectedGameVM)saveGame;
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
		/// <summary>
		/// Call back for when the load save is clicked
		/// This should invoke the passed passed in load callback
		/// Generally, this triggers the next stage after the character selection as it returns
		/// the save game back to the subscriber
		/// </summary>
		private new void ExecuteLoadSave()
		{
			SavedGameVM currentSelectedSave = CurrentSelectedSave;
			if (currentSelectedSave == null)
			{
				return;
			}
			if (_loadAction != null) _loadAction.Invoke(currentSelectedSave.Save);
		}

		/// <summary>
		/// Done is pressed, load screen is popped
		/// </summary>
		private new void ExecuteDone()
		{
			ScreenManager.PopScreen();
		}

		/// <summary>
		/// Callback for when the load is cancelled
		/// </summary>
		private void OnCancelLoadSave()
		{
		}


		/// <summary>
		/// Callback for game deletion. Disallow it for now.
		/// </summary>
		/// <param name="savedGame">Copy of the save game view</param>
		private void OnDeleteSavedGame(SavedGameVM savedGame)
		{
			InformationManager.DisplayMessage(new InformationMessage("Cannot delete saves from here"));
		}



	}

	/// <summary>
	/// Mission load screen; extends SaveLoadScreen
	/// </summary>
	class MissionLoadScreen : SaveLoadScreen
	{
		public MissionLoadScreen() : base(false)
		{
		}
	}

	/// <summary>
	/// MissionLoadGameGautletScreen extends the ScrenBase to contain the necessary information
	/// Type is overrided to MissionLoadScreen base so it can be loaded as a scren
	/// </summary>
	[OverrideView(typeof(MissionLoadScreen))]
	public class MissionLoadGameGauntletScreen : ScreenBase
	{
		/// <summary>
		/// Constructor with callback when the game is selected to be loaded
		/// </summary>
		/// <param name="loadAction">Callback when the game is selected to be loaded</param>
		public MissionLoadGameGauntletScreen(Action<SaveGameFileInfo> loadAction) {
			_loadAction = loadAction;
		}

		/// <summary>
		/// Initialize the game load selection page as done by TW
		/// </summary>
		protected override void OnInitialize()
		{
			base.OnInitialize();

			
			_datasource = new MissionLoadVM(_loadAction);
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

		/// <summary>
		/// Dereference all the initialized instances and remove layer
		/// </summary>
		protected override void OnFinalize()
		{
			base.OnFinalize();
			RemoveLayer(_gauntletLayer);
			_gauntletLayer = null;
			_datasource = null;
			_spriteCategory = null;
			_loadAction = null;
		}

		/// <summary>
		/// Class instance for the gautlet layer
		/// </summary>
		private GauntletLayer _gauntletLayer;

		/// <summary>
		/// Class instance for the SaveLoadVM
		/// </summary>
		private SaveLoadVM _datasource;

		/// <summary>
		/// Class instance for the sprite category used in the selection screen
		/// </summary>
		private SpriteCategory _spriteCategory;


		/// <summary>
		/// Class instance for the callback when a save is selected for loading
		/// </summary>
		private Action<SaveGameFileInfo> _loadAction;
	}

}
