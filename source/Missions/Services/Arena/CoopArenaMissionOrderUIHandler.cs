using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.TwoDimension;
using System.Collections.Generic;
using System.Linq;
using System;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;

namespace Missions.Services.Arena
{
    public class CoopArenaMissionOrderUIHandler : MissionView { }


    [OverrideView(typeof(CoopArenaMissionOrderUIHandler))]
    public class CoopGuantletArenaMissionOrderUIHandler : MissionView
    {
        private const string _radialOrderMovieName = "OrderRadial";

        private const string _barOrderMovieName = "OrderBar";

        private float _holdTime;

        private bool _holdExecuted;

        private OrderTroopPlacer _orderTroopPlacer;

        private GauntletLayer _gauntletLayer;

        private MissionOrderVM _dataSource;

        private IGauntletMovie _viewMovie;

        private IRoundComponent _roundComponent;

        private SpriteCategory _spriteCategory;

        private SiegeDeploymentHandler _siegeDeploymentHandler;

        private bool _isValid;

        private bool _isInitialized;

        /// <summary>
        /// Troops need to be deployed
        /// </summary>
        private bool IsDeployment = false;

        private bool _shouldTick;

        private bool _shouldInitializeFormationInfo;

        private float _latestDt;

        private bool _isTransferEnabled;

        public void ValidateInADisgustingManner()
        {
            _dataSource = new MissionOrderVM(MissionScreen.CombatCamera, 
                IsDeployment ? _siegeDeploymentHandler.PlayerDeploymentPoints.ToList() : new List<DeploymentPoint>(), 
                new Action<bool>(ToggleScreenRotation), IsDeployment, 
                new GetOrderFlagPositionDelegate(MissionScreen.GetOrderFlagPosition), 
                new OnRefreshVisualsDelegate(RefreshVisuals), 
                new ToggleOrderPositionVisibilityDelegate(SetSuspendTroopPlacer), 
                new OnToggleActivateOrderStateDelegate(OnActivateToggleOrder), 
                new OnToggleActivateOrderStateDelegate(OnDeactivateToggleOrder), 
                new OnToggleActivateOrderStateDelegate(OnTransferFinished), 
                new OnBeforeOrderDelegate(OnBeforeOrder), false);
            _gauntletLayer = new GauntletLayer(ViewOrderPriority, "GauntletLayer", false);
            SpriteData spriteData = UIResourceManager.SpriteData;
            TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
            ResourceDepot uiresourceDepot = UIResourceManager.UIResourceDepot;
            _spriteCategory = spriteData.SpriteCategories["ui_order"];
            _spriteCategory.Load(resourceContext, uiresourceDepot);
            string movieName = (BannerlordConfig.OrderType == 0) ? "OrderBar" : "OrderRadial";
            _viewMovie = this._gauntletLayer.LoadMovie(movieName, _dataSource);
            _dataSource.InputRestrictions = _gauntletLayer.InputRestrictions;
            MissionScreen.AddLayer(_gauntletLayer);
            _dataSource.AfterInitialize();
            _isValid = true;
        }

        private void ToggleScreenRotation(bool isLocked)
        {
            TaleWorlds.MountAndBlade.View.Screens.MissionScreen.SetFixedMissionCameraActive(isLocked);
        }

        private void RefreshVisuals()
        {
        }

        private void SetSuspendTroopPlacer(bool value)
        {
            _orderTroopPlacer.SuspendTroopPlacer = value;
            MissionScreen.SetOrderFlagVisibility(!value);
        }

        private void OnActivateToggleOrder()
        {
            SetLayerEnabled(true);
        }

        private void OnDeactivateToggleOrder()
        {
            SetLayerEnabled(false);
        }

        private void OnTransferFinished()
        {
        }

        private void OnBeforeOrder()
        {
            if ((MissionScreen.OrderFlag.IsVisible) && Utilities.EngineFrameNo != MissionScreen.OrderFlag.LatestUpdateFrameNo)
            {
                MissionScreen.OrderFlag.Tick(_latestDt);
            }
        }

        private void SetLayerEnabled(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_dataSource == null || _dataSource.ActiveTargetState == 0)
                {
                    _orderTroopPlacer.SuspendTroopPlacer = false;
                }
                MissionScreen.SetOrderFlagVisibility(true);
                if (_gauntletLayer != null)
                {
                    ScreenManager.SetSuspendLayer(_gauntletLayer, false);
                }
                Game.Current.EventManager.TriggerEvent(new MissionPlayerToggledOrderViewEvent(true));
                return;
            }
            _orderTroopPlacer.SuspendTroopPlacer = true;
            MissionScreen.SetOrderFlagVisibility(false);
            if (_gauntletLayer != null)
            {
                ScreenManager.SetSuspendLayer(_gauntletLayer, true);
            }
            MissionScreen.SetRadialMenuActiveState(false);
            Game.Current.EventManager.TriggerEvent(new MissionPlayerToggledOrderViewEvent(false));
        }

        public MissionOrderVM.CursorState cursorState
        {
            get
            {
                if (this._dataSource.IsFacingSubOrdersShown)
                {
                    return MissionOrderVM.CursorState.Face;
                }
                return MissionOrderVM.CursorState.Move;
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            _latestDt = dt;

            TickInput(dt);
            _dataSource.Update();
            if (this._dataSource.IsToggleOrderShown)
            {
                this._orderTroopPlacer.IsDrawingForced = this._dataSource.IsMovementSubOrdersShown;
                this._orderTroopPlacer.IsDrawingFacing = this._dataSource.IsFacingSubOrdersShown;
                this._orderTroopPlacer.IsDrawingForming = false;
                if (cursorState == MissionOrderVM.CursorState.Face)
                {
                    Vec2 orderLookAtDirection = OrderController.GetOrderLookAtDirection(base.Mission.MainAgent.Team.PlayerOrderController.SelectedFormations, base.MissionScreen.OrderFlag.Position.AsVec2);
                    base.MissionScreen.OrderFlag.SetArrowVisibility(true, orderLookAtDirection);
                }
                else
                {
                    base.MissionScreen.OrderFlag.SetArrowVisibility(false, Vec2.Invalid);
                }
                if (cursorState == MissionOrderVM.CursorState.Form)
                {
                    float orderFormCustomWidth = OrderController.GetOrderFormCustomWidth(base.Mission.MainAgent.Team.PlayerOrderController.SelectedFormations, base.MissionScreen.OrderFlag.Position);
                    base.MissionScreen.OrderFlag.SetWidthVisibility(true, orderFormCustomWidth);
                }
                else
                {
                    base.MissionScreen.OrderFlag.SetWidthVisibility(false, -1f);
                }
                if (TaleWorlds.InputSystem.Input.IsGamepadActive)
                {
                    OrderItemVM lastSelectedOrderItem = this._dataSource.LastSelectedOrderItem;
                    if (lastSelectedOrderItem == null || lastSelectedOrderItem.IsTitle)
                    {
                        base.MissionScreen.SetRadialMenuActiveState(false);
                        if (this._orderTroopPlacer.SuspendTroopPlacer && this._dataSource.ActiveTargetState == 0)
                        {
                            this._orderTroopPlacer.SuspendTroopPlacer = false;
                        }
                    }
                    else
                    {
                        base.MissionScreen.SetRadialMenuActiveState(true);
                        if (!this._orderTroopPlacer.SuspendTroopPlacer)
                        {
                            this._orderTroopPlacer.SuspendTroopPlacer = true;
                        }
                    }
                }
            }
            else if (this._dataSource.TroopController.IsTransferActive)
            {
                this._gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            }
            else
            {
                if (!this._orderTroopPlacer.SuspendTroopPlacer)
                {
                    this._orderTroopPlacer.SuspendTroopPlacer = true;
                }
                this._gauntletLayer.InputRestrictions.ResetInputRestrictions();
            }
            base.MissionScreen.OrderFlag.IsTroop = (this._dataSource.ActiveTargetState == 0);
            this.TickOrderFlag();
        }



        public override void AfterStart()
        {
            ValidateInADisgustingManner();
        }

        public override void OnMissionScreenActivate()
        {
            base.OnMissionScreenActivate();
            if (_dataSource == null) return;

            MissionScreen.SceneLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("MissionOrderHotkeyCategory"));
            _dataSource.AfterInitialize();
            MissionScreen.OrderFlag = new OrderFlag(Mission, MissionScreen);
            _orderTroopPlacer = Mission.GetMissionBehavior<OrderTroopPlacer>();
            MissionScreen.SetOrderFlagVisibility(false);
            _isInitialized = true;
        }

        private void TickInput(float dt)
        {
            if (Input.IsGameKeyDown(86) && !_dataSource.IsToggleOrderShown)
            {
                _holdTime += dt;
                if (_holdTime >= 0)
                {
                    _dataSource.OpenToggleOrder(true, !_holdExecuted);
                    _holdExecuted = true;
                }
            }
            else
            {
                if (_holdExecuted && this._dataSource.IsToggleOrderShown)
                {
                    _dataSource.TryCloseToggleOrder(false);
                }
                _holdExecuted = false;
                _holdTime = 0f;
            }

            if (Input.IsGameKeyPressed(67))
            {
                _dataSource.ViewOrders();
            }
        }

        private void TickOrderFlag()
        {
            if (MissionScreen.OrderFlag.IsVisible && Utilities.EngineFrameNo != MissionScreen.OrderFlag.LatestUpdateFrameNo)
            {
                MissionScreen.OrderFlag.Tick(_latestDt);
            }
        }
    }
}
