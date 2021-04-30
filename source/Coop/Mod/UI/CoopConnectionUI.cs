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
using Coop.Mod.UI;
using TaleWorlds.InputSystem;
using TaleWorlds.TwoDimension;

namespace Coop.Mod
{
	class CoopConnectionUI : ScreenBase
	{
        private CoopConnectMenuVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private IGauntletMovie _gauntletMovie;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _dataSource = new CoopConnectMenuVM();
            _gauntletLayer = new GauntletLayer(100)
            {
                IsFocusLayer = true
            };
            AddLayer(_gauntletLayer);
            _gauntletLayer.InputRestrictions.SetInputRestrictions();
            _gauntletMovie = _gauntletLayer.LoadMovie("CoopConnectionUIMovie", _dataSource);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            _gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_gauntletLayer);
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            RemoveLayer(_gauntletLayer);
            _dataSource = null;
            _gauntletLayer = null;
        }

    }
}