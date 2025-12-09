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
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;

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
            try { base.ExecuteSelection(); } catch { _onSelection(this); }
        }

        public void ExecuteSelect()
        {
            try { base.ExecuteSelection(); } catch { _onSelection(this); }
        }

        public void ExecuteToggleSelect()
        {
            try { base.ExecuteSelection(); } catch { _onSelection(this); }
        }

        public void ExecuteOpen()
        {
            try { base.ExecuteSelection(); } catch { _onSelection(this); }
        }

        public new void ExecuteSaveLoad()
        {
            if (Game.Current != null)
            {
                ScreenManager.PopScreen();
                GameStateManager.Current.CleanStates(0);
                GameStateManager.Current = TaleWorlds.MountAndBlade.Module.CurrentModule.GlobalGameStateManager;
            }

            InformationManager.DisplayMessage(new InformationMessage($"Hébergement en cours sur la sauvegarde {Save.Name}..."));

            MessageBroker.Instance.Publish(this, new HostSaveGame(Save.Name));
        }
	}

    class CoopLoadUI : SaveLoadVM
    {
        private SelectedGameVM _currentSelectedOverride;
        public CoopLoadUI() : base(false, false)
        {
            EnsureSaveGroup();
            var list = GetSavedGames();
            list?.Clear();
            var saveFiles = MBSaveLoad.GetSaveFiles();
            if (saveFiles == null || saveFiles.Length == 0)
            {
                try
                {
                    var driver = new TaleWorlds.SaveSystem.FileDriver();
                    var infos = driver.GetSaveGameFileInfos();
                    saveFiles = infos?.OrderByDescending(i => i.MetaData?.GetCreationTime() ?? DateTime.MinValue).ToArray() ?? Array.Empty<SaveGameFileInfo>();
                }
                catch
                {
                    saveFiles = Array.Empty<SaveGameFileInfo>();
                }
            }
            InformationManager.DisplayMessage(new InformationMessage($"Sauvegardes détectées: {saveFiles.Length}"));
            for (int i = 0; i < saveFiles.Length; i++)
            {
                var item = new SelectedGameVM(saveFiles[i], IsSaving, new Action<SavedGameVM>(OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(OnCancelLoadSave), new Action(ExecuteDone));
                list?.Add(item);
                try { item.RefreshValues(); } catch { }
            }
            try
            {
                var group = typeof(SaveLoadVM).GetProperty("SelectedSaveGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(this);
                if (group != null)
                {
                    var filteredProp = group.GetType().GetProperty("FilteredSavedGames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    filteredProp?.SetValue(group, list);
                    var onPc = group.GetType().GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    onPc?.Invoke(group, new object[] { "SavedGamesList" });
                    onPc?.Invoke(group, new object[] { "FilteredSavedGames" });
                    var vmOnPc = typeof(SaveLoadVM).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    vmOnPc?.Invoke(this, new object[] { "SelectedSaveGroup" });
                    vmOnPc?.Invoke(this, new object[] { "SaveGroups" });
                }
            }
            catch { }
            try
            {
                var firstItem = list?.FirstOrDefault();
                if (firstItem != null)
                {
                    var methods = firstItem.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name.StartsWith("Execute", StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.Name)
                        .Distinct()
                        .Take(6)
                        .ToArray();
                    InformationManager.DisplayMessage(new InformationMessage($"Item methods: {string.Join(", ", methods)}"));
                }
            }
            catch { }
            LogDiagnostics();
            OnSaveSelection(list?.FirstOrDefault());
            RefreshValues();
        }

        private void OnSaveSelection(SavedGameVM saveGame)
        {
            SelectedGameVM save = (SelectedGameVM)saveGame;
            if (save != _currentSelectedOverride)
            {
                if (_currentSelectedOverride != null)
                {
                    _currentSelectedOverride.IsSelected = false;
                }
                _currentSelectedOverride = save;
                if (_currentSelectedOverride != null)
                {
                    _currentSelectedOverride.IsSelected = true;
                }
                IsActionEnabled = _currentSelectedOverride != null;
                try
                {
                    var vmOnPc = typeof(SaveLoadVM).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var selectedSaveProp = typeof(SaveLoadVM).GetProperty("SelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    selectedSaveProp?.SetValue(this, save);
                    vmOnPc?.Invoke(this, new object[] { "SelectedSave" });
                    vmOnPc?.Invoke(this, new object[] { "IsActionEnabled" });
                    var anySelectedProp = typeof(SaveLoadVM).GetProperty("IsAnyItemSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    anySelectedProp?.SetValue(this, _currentSelectedOverride != null);
                    vmOnPc?.Invoke(this, new object[] { "IsAnyItemSelected" });

                    var currentSelectedProp = typeof(SaveLoadVM).GetProperty("CurrentSelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    currentSelectedProp?.SetValue(this, save);
                    vmOnPc?.Invoke(this, new object[] { "CurrentSelectedSave" });

                    var group = typeof(SaveLoadVM).GetProperty("SelectedSaveGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(this);
                    if (group != null)
                    {
                        var groupSelProp = group.GetType().GetProperty("SelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        groupSelProp?.SetValue(group, save);
                        var groupOnPc = group.GetType().GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        groupOnPc?.Invoke(group, new object[] { "SelectedSave" });
                        try
                        {
                            var list = GetSavedGames();
                            var idx = list != null && save != null ? list.IndexOf(save) : -1;
                            var groupIdxProp = group.GetType().GetProperty("SelectedSaveIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (groupIdxProp != null && idx >= 0)
                            {
                                groupIdxProp.SetValue(group, idx);
                                groupOnPc?.Invoke(group, new object[] { "SelectedSaveIndex" });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        public new void ExecuteLoadSave()
        {
            SelectedGameVM currentSelectedSave = _currentSelectedOverride;
            if (currentSelectedSave == null)
            {
                try
                {
                    var selectedSaveProp = typeof(SaveLoadVM).GetProperty("SelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    currentSelectedSave = selectedSaveProp?.GetValue(this) as SelectedGameVM;
                    if (currentSelectedSave == null)
                    {
                        var currentSelectedProp = typeof(SaveLoadVM).GetProperty("CurrentSelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        currentSelectedSave = currentSelectedProp?.GetValue(this) as SelectedGameVM;
                    }
                }
                catch { }
            }
            if (currentSelectedSave == null)
            {
                return;
            }
            currentSelectedSave.ExecuteSaveLoad();
        }

		public void ExecuteDeleteSelectedSave()
		{
			try { DeleteSelectedSave(); } catch { }
		}

		public void ExecuteCancel()
		{
			try { ExecuteDone(); } catch { }
		}

		public bool SelectFirst()
		{
			var list = GetSavedGames();
			var first = list?.FirstOrDefault();
			if (first == null) return false;
			OnSaveSelection(first);
			return true;
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

        private MBBindingList<SavedGameVM> GetSavedGames()
        {
            object group = null;
            var saveGroupsProp = typeof(SaveLoadVM).GetProperty("SaveGroups", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var groupsObj = saveGroupsProp?.GetValue(this);
            var saveGroupsField = typeof(SaveLoadVM).GetField("_saveGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            if (groupsObj != null)
            {
                var countProp = groupsObj.GetType().GetProperty("Count");
                var indexer = groupsObj.GetType().GetProperty("Item");
                var count = (int)(countProp?.GetValue(groupsObj) ?? 0);
                group = count > 0 ? indexer?.GetValue(groupsObj, new object[] { 0 }) : null;
            }
            if (group == null)
            {
                EnsureSaveGroup();
                groupsObj = saveGroupsProp?.GetValue(this);
                if (groupsObj != null)
                {
                    var countProp = groupsObj.GetType().GetProperty("Count");
                    var indexer = groupsObj.GetType().GetProperty("Item");
                    var count = (int)(countProp?.GetValue(groupsObj) ?? 0);
                    group = count > 0 ? indexer?.GetValue(groupsObj, new object[] { 0 }) : null;
                }
                if (group == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Aucun groupe de sauvegardes disponible"));
                    return null;
                }
            }
            var savedGamesProp = group.GetType().GetProperty("SavedGamesList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (MBBindingList<SavedGameVM>)savedGamesProp?.GetValue(group);
        }

        private void EnsureSaveGroup()
        {
            var saveGroupsProp = typeof(SaveLoadVM).GetProperty("SaveGroups", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var groupsObj = saveGroupsProp?.GetValue(this);
            var saveGroupsField = typeof(SaveLoadVM).GetField("_saveGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            var saveGroupsType = saveGroupsProp?.PropertyType ?? saveGroupsField?.FieldType;
            if (saveGroupsType == null) return;
            var groupItemType = saveGroupsType.IsGenericType ? saveGroupsType.GetGenericArguments()[0] : null;
            if (groupItemType == null) return;
            if (groupsObj == null)
            {
                object newList = null;
                try { newList = Activator.CreateInstance(saveGroupsType); } catch { }
                if (newList == null)
                {
                    var listType = typeof(MBBindingList<>).MakeGenericType(groupItemType);
                    try { newList = Activator.CreateInstance(listType); } catch { }
                }
                if (newList == null) return;
                var setOk = true;
                try { saveGroupsProp?.SetValue(this, newList); }
                catch { setOk = false; }
                if (!setOk || saveGroupsProp?.GetValue(this) == null)
                {
                    try { saveGroupsField?.SetValue(this, newList); } catch { }
                }
                groupsObj = saveGroupsProp?.GetValue(this) ?? saveGroupsField?.GetValue(this) ?? newList;
            }
            var countProp = groupsObj.GetType().GetProperty("Count");
            var addMethod = groupsObj.GetType().GetMethod("Add");
            var count = (int)(countProp?.GetValue(groupsObj) ?? 0);
            if (count == 0)
            {
                object groupInstance = null;
                try { groupInstance = Activator.CreateInstance(groupItemType); } catch { }
                if (groupInstance == null)
                {
                    try { groupInstance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(groupItemType); } catch { }
                }
                if (groupInstance == null) return;
                var savedGamesProp = groupItemType.GetProperty("SavedGamesList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var savedGamesAltProp = groupItemType.GetProperty("SavedGames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var filteredProp = groupItemType.GetProperty("FilteredSavedGames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var savedGamesList = new MBBindingList<SavedGameVM>();
                savedGamesProp?.SetValue(groupInstance, savedGamesList);
                savedGamesAltProp?.SetValue(groupInstance, savedGamesList);
                filteredProp?.SetValue(groupInstance, savedGamesList);
                var nameProp = groupItemType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                nameProp?.SetValue(groupInstance, "Campagnes");
                var isSelectedProp = groupItemType.GetProperty("IsSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                isSelectedProp?.SetValue(groupInstance, true);
                addMethod?.Invoke(groupsObj, new object[] { groupInstance });
                var onPc = groupItemType.GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                onPc?.Invoke(groupInstance, new object[] { "SavedGamesList" });
                onPc?.Invoke(groupInstance, new object[] { "FilteredSavedGames" });
            }
            var selectedGroupProp = typeof(SaveLoadVM).GetProperty("SelectedSaveGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selectedGroupProp != null)
            {
                var indexer = groupsObj.GetType().GetProperty("Item");
                var first = indexer?.GetValue(groupsObj, new object[] { 0 });
                selectedGroupProp.SetValue(this, first);
                var onPcVm = typeof(SaveLoadVM).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                onPcVm?.Invoke(this, new object[] { "SaveGroups" });
                onPcVm?.Invoke(this, new object[] { "SelectedSaveGroup" });
                var onPcGroup = first?.GetType().GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                onPcGroup?.Invoke(first, new object[] { "IsSelected" });
            }
        }

        private void LogDiagnostics()
        {
            try
            {
                var saveGroupsProp = typeof(SaveLoadVM).GetProperty("SaveGroups", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var groupsObj = saveGroupsProp?.GetValue(this);
                var countProp = groupsObj?.GetType().GetProperty("Count");
                var groupsCount = groupsObj != null ? (int)(countProp?.GetValue(groupsObj) ?? 0) : 0;
                InformationManager.DisplayMessage(new InformationMessage($"Groupes: {groupsCount}"));
                var selectedGroup = typeof(SaveLoadVM).GetProperty("SelectedSaveGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(this);
                InformationManager.DisplayMessage(new InformationMessage($"Groupe sélectionné: {(selectedGroup != null)}"));
                if (selectedGroup == null && groupsObj != null)
                {
                    var indexer = groupsObj.GetType().GetProperty("Item");
                    selectedGroup = indexer?.GetValue(groupsObj, new object[] { 0 });
                }
                if (selectedGroup != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Type groupe: {selectedGroup.GetType().FullName}"));
                    var props = selectedGroup.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var p in props)
                    {
                        var pt = p.PropertyType;
                        if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(MBBindingList<>))
                        {
                            var val = p.GetValue(selectedGroup);
                            var cProp = val?.GetType().GetProperty("Count");
                            var c = val != null ? (int)(cProp?.GetValue(val) ?? 0) : 0;
                            InformationManager.DisplayMessage(new InformationMessage($"{p.Name}: {c}"));
                        }
                        else if (pt == typeof(string) || pt == typeof(bool))
                        {
                            var val = p.GetValue(selectedGroup);
                            InformationManager.DisplayMessage(new InformationMessage($"Groupe {p.Name}: {val}"));
                        }
                        else if (pt == typeof(int))
                        {
                            var val = p.GetValue(selectedGroup);
                            InformationManager.DisplayMessage(new InformationMessage($"Groupe {p.Name}: {val}"));
                        }
                    }
                }
                var vmProps = typeof(SaveLoadVM).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                InformationManager.DisplayMessage(new InformationMessage($"Type VM: {GetType().FullName}"));
                foreach (var p in vmProps)
                {
                    var pt = p.PropertyType;
                    if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(MBBindingList<>))
                    {
                        var val = p.GetValue(this);
                        var cProp = val?.GetType().GetProperty("Count");
                        var c = val != null ? (int)(cProp?.GetValue(val) ?? 0) : 0;
                        InformationManager.DisplayMessage(new InformationMessage($"VM {p.Name}: {c}"));
                    }
                    else if (pt == typeof(string) || pt == typeof(bool))
                    {
                        var val = p.GetValue(this);
                        InformationManager.DisplayMessage(new InformationMessage($"VM {p.Name}: {val}"));
                    }
                }
                try
                {
                    var selSave = typeof(SaveLoadVM).GetProperty("SelectedSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(this) as SavedGameVM;
                    InformationManager.DisplayMessage(new InformationMessage($"VM SelectedSave: {(selSave != null ? selSave.Save?.Name : "<null>")}"));
                }
                catch { }
            }
            catch { }
        }
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
            _spriteCategory = spriteData.SpriteCategories["ui_saveload"];
            

            _gauntletLayer = new GauntletLayer("SaveLoadLayer", 1, true);
            _gauntletLayer.LoadMovie("SaveLoadScreen", _dataSource);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.IsFocusLayer = true;
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            ScreenManager.TrySetFocus(_gauntletLayer);
            AddLayer(_gauntletLayer);
            try { _dataSource.SelectFirst(); } catch { }
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
            Utilities.SetForceVsync(false);
        }

        
        

        

        

        
    }

}
