using Common.Messaging;
using Common.Network;
using GameInterface.Services.UI.Messages;
using SandBox.View;
using SandBox.ViewModelCollection.SaveLoad;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

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
            if (IsCorrupted || IsDisabled)
                return;

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Server Password",
                "Set an optional password for this server. Leave it blank to allow anyone to join.",
                true,
                true,
                "Host",
                "Cancel",
                StartHosting,
                () => { },
                shouldInputBeObfuscated: true,
                textCondition: ValidatePassword));
        }

        private static Tuple<bool, string> ValidatePassword(string password)
        {
            bool valid = ConnectionPassword.IsValid(password);
            return Tuple.Create(valid, valid
                ? string.Empty
                : $"Password cannot exceed {ConnectionPassword.MaxLength} characters");
        }

        private void StartHosting(string password)
        {
            if (Game.Current != null)
            {
                ScreenManager.PopScreen();
                GameStateManager.Current.CleanStates(0);
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }

            // The handler decides spawn-managed (Steam) vs in-process hosting.
            MessageBroker.Instance.Publish(this, new AttemptHost(Save.Name, password ?? string.Empty));
        }
	}

	class CoopLoadUI : SaveLoadVM
	{
		private new SelectedGameVM CurrentSelectedSave;
        public CoopLoadUI() : base(false, false)
        {
            InitializeSandboxSaves();
            OnSaveSelection(GetSavedGames()?.FirstOrDefault());
            RefreshValues();
        }

        // Replaces SaveLoadVM.InitializeAsync (async as of v1.4.7): synchronous so the
        // list exists before the screen shows, restricted to sandbox campaigns, and
        // builds SelectedGameVM entries directly instead of re-wrapping the base list.
        private void InitializeSandboxSaves()
        {
            var categorizedGroupName = new TextObject("{=nVGqjtaa}Campaign {ID}", null);
            var uncategorizedGroupName = new TextObject("{=uncategorized_save}Uncategorized", null);

            var saves = MBSaveLoad.GetSaveFiles(save => !save.IsCorrupted && IsSandboxSave(save));

            int campaignNumber = 0;
            foreach (var group in saves
                .GroupBy(save => save.MetaData.GetUniqueGameId())
                .OrderByDescending(g => g.Max(save => save.MetaData.GetCreationTime())))
            {
                var groupVM = new SavedGameGroupVM();
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    groupVM.IdentifierID = uncategorizedGroupName.ToString();
                }
                else
                {
                    campaignNumber++;
                    categorizedGroupName.SetTextVariable("ID", campaignNumber);
                    groupVM.IdentifierID = categorizedGroupName.ToString();
                }

                foreach (var save in group.OrderByDescending(s => s.MetaData.GetCreationTime()))
                {
                    groupVM.SavedGamesList.Add(new SelectedGameVM(save, IsSaving, OnDeleteSavedGame, OnSaveSelection, OnCancelLoadSave, ExecuteDone));
                }

                SaveGroups.Add(groupVM);
            }

            CanCreateNewSave = !MBSaveLoad.IsMaxNumberOfSavesReached();
            IsSearchAvailable = true;
        }

        // Sandbox sessions start with the StoryMode module deactivated
        // (StoryModeViewSubModule.OnBeforeGameStart), so a save listing StoryMode
        // among its modules is a story campaign.
        private static bool IsSandboxSave(SaveGameFileInfo save)
        {
            return !save.MetaData.GetModules()
                .Any(module => string.Equals(module, "StoryMode", StringComparison.OrdinalIgnoreCase));
        }

        private new void OnSaveSelection(SavedGameVM saveGame)
        {
            var save = (SelectedGameVM)saveGame;
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
            var currentSelectedSave = CurrentSelectedSave;
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

        private new void OnCancelLoadSave()
        {
        }

        private new void OnDeleteSavedGame(SavedGameVM savedGame)
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

        private MBBindingList<SavedGameVM> GetSavedGames() => SaveGroups.FirstOrDefault()?.SavedGamesList;
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
            ResourceDepot uiresourceDepot = UIResourceManager.ResourceDepot;
            _spriteCategory = spriteData.SpriteCategories["ui_saveload"];
            _spriteCategory.Load(resourceContext, uiresourceDepot);

            _gauntletLayer = new GauntletLayer("GauntletLayer", 1);
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
