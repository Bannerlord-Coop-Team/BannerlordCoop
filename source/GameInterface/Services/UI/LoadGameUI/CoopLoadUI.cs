using TaleWorlds.Core;
using SandBox.View;
using SandBox.ViewModelCollection.SaveLoad;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using System.Linq;
using TaleWorlds.SaveSystem;
using TaleWorlds.ScreenSystem;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.InputSystem;
using TaleWorlds.TwoDimension;
using Common.Messaging;
using GameInterface.Services.UI.Messages;

namespace Coop.UI.LoadGameUI
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

        public new void ExecuteDelete()
		{
			_onDelete(this);
		}

		public new void ExecuteSelection()
		{
			_onSelection(this);
		}

		public new void ExecuteSaveLoad()
		{
            if (Game.Current != null)
            {
                ScreenManager.PopScreen();
                GameStateManager.Current.CleanStates(0);
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }

            MessageBroker.Instance.Publish(this, new HostSave(Save.Name));
        }
	}

	class CoopLoadUI : SaveLoadVM
	{
		private new SelectedGameVM CurrentSelectedSave;
        public CoopLoadUI() : base(false, false)
        {
            GetSavedGames().Clear();
            SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles();
            for (int i = 0; i < saveFiles.Length; i++)
            {
                SelectedGameVM item = new SelectedGameVM(saveFiles[i], IsSaving, new Action<SavedGameVM>(OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(OnCancelLoadSave), new Action(ExecuteDone));
                GetSavedGames().Add(item);
            }
            OnSaveSelection(GetSavedGames().FirstOrDefault());
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
                IsActionEnabled = CurrentSelectedSave != null;
            }
        }

        public new void ExecuteLoadSave()
        {
            SelectedGameVM currentSelectedSave = CurrentSelectedSave;
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

	public class CoopLoadScreen : SaveLoadScreen
	{
		public CoopLoadScreen() : base(false)
		{
		}
	}

	[OverrideView(typeof(CoopLoadScreen))]
	public class CoopLoadGameGauntletScreen : ScreenBase
	{
        private GauntletLayer _gauntletLayer;
        private CoopLoadUI _dataSource;
        private SpriteCategory _spriteCategory;

        public CoopLoadGameGauntletScreen() { }

		protected override void OnInitialize()
		{
			base.OnInitialize();
			_dataSource = new CoopLoadUI();
            _dataSource.SetDeleteInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Delete"));
            _dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
            _dataSource.SetCancelInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Exit"));

            Game.Current?.GameStateManager.RegisterActiveStateDisableRequest(this);

            SpriteData spriteData = UIResourceManager.SpriteData;
            TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
            ResourceDepot uiresourceDepot = UIResourceManager.UIResourceDepot;
            _spriteCategory = spriteData.SpriteCategories["ui_saveload"];
            _spriteCategory.Load(resourceContext, uiresourceDepot);

            _gauntletLayer = new GauntletLayer(1, "GauntletLayer");
            _gauntletLayer.LoadMovie("SaveLoadScreen", _dataSource);
			_gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.IsFocusLayer = true;
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            ScreenManager.TrySetFocus(_gauntletLayer);
            AddLayer(_gauntletLayer);
		}

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (!_dataSource.IsBusyWithAnAction)
            {
                if (_gauntletLayer.Input.IsHotKeyReleased("Exit"))
                {
                    _dataSource.ExecuteDone();
                    return;
                }
                if (_gauntletLayer.Input.IsHotKeyPressed("Confirm") && !_gauntletLayer.IsFocusedOnInput())
                {
                    _dataSource.ExecuteLoadSave();
                    return;
                }
                if (_gauntletLayer.Input.IsHotKeyPressed("Delete") && !_gauntletLayer.IsFocusedOnInput())
                {
                    _dataSource.DeleteSelectedSave();
					return;
                }
            }
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            Game.Current?.GameStateManager.UnregisterActiveStateDisableRequest(this);
            RemoveLayer(_gauntletLayer);
            _gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_gauntletLayer);
            _gauntletLayer = null;
            _dataSource.OnFinalize();
            _dataSource = null;
            _spriteCategory.Unload();
            Utilities.SetForceVsync(false);
        }
    }

}
