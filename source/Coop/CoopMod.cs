using Common;
using Common.Logging;
using Coop.Lib.NoHarmony;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using Module = TaleWorlds.MountAndBlade.Module;
using System.Net;
using Common.Messaging;
using GameInterface.Services.UI.Messages;
using SandBox.ViewModelCollection.SaveLoad;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.TwoDimension;
using TaleWorlds.InputSystem;
using GameInterface.Services.GameState.Messages;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Coop
{
    internal class CoopMod : NoHarmonyLoader
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();

        public static bool CoopEnabled;

        public static InitialStateOption CoopCampaign;

        public static InitialStateOption JoinCoopGame;

        private static ILogger Logger;
        public static object coopExperience;
        private static bool UiReady;
        private static readonly List<string> PendingInfoMessages = new List<string>();

        public CoopMod()
        {
            MBDebug.DisableLogging = false;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        internal static void StartGameFromSaveReflection(LoadResult loadResult)
        {
            InformationManager.DisplayMessage(new InformationMessage("Sauvegarde chargée, initialisation de la campagne"));
            var asmTypes = AppDomain.CurrentDomain.GetAssemblies();
            Type sbMgrType = null;
            foreach (var a in asmTypes)
            {
                try
                {
                    sbMgrType = a.GetType("SandBox.SandBoxGameManager", throwOnError: false);
                    if (sbMgrType != null) break;
                }
                catch { }
            }
            object mgrInstance = sbMgrType != null ? Activator.CreateInstance(sbMgrType, loadResult) : null;
            MBGameManager.StartNewGame((MBGameManager)mgrInstance);
            MouseManager.ShowCursor(false);
        }

        internal static void TryLoadSaveViaReflection(SaveGameFileInfo save)
        {
            var asmTypes = AppDomain.CurrentDomain.GetAssemblies();
            Type helperType = null;
            foreach (var a in asmTypes)
            {
                try
                {
                    helperType = a.GetType("SandBox.SandBoxSaveHelper", throwOnError: false);
                    if (helperType != null) break;
                }
                catch { }
            }
            var method = helperType?.GetMethod("TryLoadSave", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                var onLoaded = new Action<LoadResult>(StartGameFromSaveReflection);
                method.Invoke(null, new object[] { save, onLoaded, null });
            }
        }

        private static string ClientServerModeMessage = "";

        private bool isServer = false;
        public override void NoHarmonyInit() 
        {
            AssemblyHellscape.CreateAssemblyBindingRedirects();

            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();
            
            if (args.Contains("/server"))
            {
                isServer = true;
            }
            else if (args.Contains("/client"))
            {
                isServer = false;
            }

            try { GameInterface.ModInformation.IsServer = isServer; } catch { }

            ClientServerModeMessage = isServer ? "Coop: Serveur [Release]" : "Coop: Client [Release]";

            SetupLogging();
            ValidateDependencies();
            EnableExceptionTracing();

            GameLoopRunner.Instance.SetGameLoopThread();

            try
            {
                var moduleDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var noharmonyPath = System.IO.Path.Combine(moduleDir ?? string.Empty, "NoHarmony_2952.txt");
                try { File.Delete(noharmonyPath); } catch { }
                LogFile = noharmonyPath;
            }
            catch { }
        }

        private void SetupLogging()
        {
            var outputTemplate = "[( {ProcessId} ) {Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

            var logsDir = "C:\\ProgramData\\Mount and Blade II Bannerlord\\logs";
            var serverFilePath = System.IO.Path.Combine(logsDir, "Coop_server.log");
            var clientFilePath = System.IO.Path.Combine(logsDir, "Coop_client.log");
            var moduleDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var serverAltFilePath = System.IO.Path.Combine(moduleDir ?? string.Empty, "Coop_server.log");
            var clientAltFilePath = System.IO.Path.Combine(moduleDir ?? string.Empty, "Coop_client.log");

            Directory.CreateDirectory(logsDir);
            try
            {
                // reset both files for clean session
                File.Delete(serverFilePath);
                File.Delete(clientFilePath);
            }
            catch (IOException) { }

            try
            {
                File.AppendAllText(serverFilePath, "BOOTSTRAP\n");
                File.AppendAllText(clientFilePath, "BOOTSTRAP\n");
                File.AppendAllText(serverAltFilePath, "BOOTSTRAP\n");
                File.AppendAllText(clientAltFilePath, "BOOTSTRAP\n");

                LogManager.Configuration
                    .Enrich.WithProcessId()
                    .WriteTo.Debug(outputTemplate: outputTemplate)
                    // always write both server and client logs
                    .WriteTo.File(serverFilePath, outputTemplate: outputTemplate)
                    .WriteTo.File(clientFilePath, outputTemplate: outputTemplate)
                    .WriteTo.File(serverAltFilePath, outputTemplate: outputTemplate)
                    .WriteTo.File(clientAltFilePath, outputTemplate: outputTemplate)
                    .MinimumLevel.Verbose();
                LogManager.Build();
                Logger = LogManager.GetLogger<CoopMod>();
                Logger.Verbose("Coop Mod Module Started");

                OutputSinkManager.AddLogEventCallback(logEvent =>
                {
                    if (logEvent.Level < Serilog.Events.LogEventLevel.Information) return;

                    var hasContext = logEvent.Properties.TryGetValue("SourceContext", out var ctx);
                    var src = hasContext ? ctx.ToString().Trim('"') : "Server";

                    var sw = new System.IO.StringWriter();
                    logEvent.RenderMessage(sw);
                    var msg = sw.ToString();

                    // Filter to server-related namespaces to avoid spam
                    if (src.StartsWith("Coop.Core.Server") || src.StartsWith("Common.Network") || src.StartsWith("Coop.Core.Common.Network"))
                    {
                        GameLoopRunner.RunOnMainThread(() =>
                        {
                            if (UiReady)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[Serveur] {msg}"));
                            }
                            else
                            {
                                PendingInfoMessages.Add($"[Serveur] {msg}");
                            }
                        });
                    }
                    else if (src.StartsWith("Coop.Core.Client"))
                    {
                        GameLoopRunner.RunOnMainThread(() =>
                        {
                            if (UiReady)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[Client] {msg}"));
                            }
                            else
                            {
                                PendingInfoMessages.Add($"[Client] {msg}");
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                var msg = ("LOG_INIT_FAIL: " + ex.GetType().Name + ": " + ex.Message + "\n");
                try { File.AppendAllText(serverFilePath, msg); } catch {}
                try { File.AppendAllText(clientFilePath, msg); } catch {}
                try { File.AppendAllText(serverAltFilePath, msg); } catch {}
                try { File.AppendAllText(clientAltFilePath, msg); } catch {}
            }
        }

        private void ValidateDependencies()
        {
            var baseDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var deps = new string[]
            {
                "Serilog.dll",
                "Serilog.Sinks.File.dll",
                "Serilog.Sinks.Debug.dll",
                "Serilog.Enrichers.Process.dll",
                "Autofac.dll",
                "LiteNetLib.dll",
                "Newtonsoft.Json.dll",
                "protobuf-net.dll",
                "protobuf-net.Core.dll",
                "Mono.Cecil.dll",
                "Mono.Cecil.Mdb.dll",
                "Mono.Cecil.Pdb.dll",
                "Mono.Cecil.Rocks.dll",
                "MonoMod.Backports.dll",
                "MonoMod.Core.dll",
                "MonoMod.Iced.dll",
                "MonoMod.ILHelpers.dll",
                "MonoMod.RuntimeDetour.dll",
                "MonoMod.Utils.dll"
            };

            foreach (var d in deps)
            {
                var p = System.IO.Path.Combine(baseDir ?? string.Empty, d);
                if (!File.Exists(p))
                {
                    Logger?.Fatal("Dependency missing: {Dependency}", p);
                }
                else
                {
                    Logger?.Verbose("Dependency present: {Dependency}", p);
                }
            }
        }

        private void EnableExceptionTracing()
        {
            AppDomain.CurrentDomain.FirstChanceException += (sender, e) =>
            {
                Logger?.Fatal(e.Exception, "FirstChanceException");
            };
        }

        
        public override void NoHarmonyLoad()
        {
            CoopEnabled = true;

            Updateables.Add(GameLoopRunner.Instance);


            // Skip startup splash screen
#if DEBUG
            typeof(Module).GetField(
                                "_splashScreenPlayed",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(Module.CurrentModule, true);
#endif
            #region ButtonAssignment

            CoopCampaign = new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject("Coop placeholder"),
                    9990,
                    () =>
                    {
                        MessageBroker.Instance.Publish(this, new HostSaveGame("coop_autosave"));
                    },
                    () => { return (false, new TextObject("")); }
                );

            Module.CurrentModule.AddInitialStateOption(CoopCampaign);

            JoinCoopGame = new InitialStateOption(
                    "Join CoOp",
                    new TextObject("Join coop placeholder"),
                    9991,
                    () =>
                    {
                        JoinWindow();
                    },
                    () => { return (false, new TextObject("")); }
                );

            Module.CurrentModule.AddInitialStateOption(JoinCoopGame);
            
            #endregion

            InformationManager.DisplayMessage(new InformationMessage(isServer ? "Coop Serveur (Release)" : "Coop Client (Release)"));

            try
            {
                var t = Type.GetType("Coop.Core.CoopartiveMultiplayerExperience, Coop.Core", throwOnError: false);
                coopExperience = t != null ? Activator.CreateInstance(t) : null;
                Logger?.Verbose("CoopartiveMultiplayerExperience initialisée: {Instance}", coopExperience != null);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Échec d'initialisation de CoopartiveMultiplayerExperience");
            }

            try
            {
                GameInterface.Services.Naval.Patches.NavalDlcDynamicPatches.Apply();
                Logger?.Information("Patch naval dynamique appliqué");
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Échec application patch naval dynamique");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(ClientServerModeMessage));
            UiReady = true;
            if (PendingInfoMessages.Count > 0)
            {
                foreach (var m in PendingInfoMessages)
                {
                    try { InformationManager.DisplayMessage(new InformationMessage(m)); } catch { }
                }
                PendingInfoMessages.Clear();
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            CoopEnabled = false;
        }

        private bool m_IsFirstTick = true;
        private bool m_CampaignReadyPublished = false;
        protected override void OnApplicationTick(float dt)
        {
            if(m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                
                m_IsFirstTick = false;
            }    
            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);

            if (!m_CampaignReadyPublished)
            {
                try
                {
                    if (TaleWorlds.CampaignSystem.Campaign.Current != null && TaleWorlds.Core.Game.Current != null)
                    {
                        Logger?.Information("Publishing CampaignReady (Tick fallback)");
                        m_CampaignReadyPublished = true;
                        Common.Messaging.MessageBroker.Instance.Publish(this, new CampaignReady());
                    }
                }
                catch { }
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Logger?.Fatal(ex, "Unhandled exception");
            Logger?.Fatal(ex.StackTrace);
            Serilog.Log.CloseAndFlush();
        }

        internal static void JoinWindow()
        {
            var typeName = "GameInterface.Services.UI.CoopConnectionUI, GameInterface";
            var t = Type.GetType(typeName, throwOnError: false);
            if (t != null && typeof(ScreenBase).IsAssignableFrom(t))
            {
                var screen = (ScreenBase)Activator.CreateInstance(t);
                ScreenManager.PushScreen(screen);
                return;
            }

            var layer = new GauntletLayer("CoopConnection", 100, true) { IsFocusLayer = true };
            var vm = new CoopConnectionVM(
                onCancel: () =>
                {
                    try { ScreenManager.TryLoseFocus(layer); } catch { }
                    try { ScreenManager.TopScreen.RemoveLayer(layer); } catch { }
                },
                onConnect: (ip, port, password) =>
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Connexion à {ip}:{port}"));
                    var broker = MessageBroker.Instance;
                    IPAddress addr;
                    if (!IPAddress.TryParse(ip, out addr))
                    {
                        try
                        {
                            var hostEntry = System.Net.Dns.GetHostEntry(ip);
                            addr = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) 
                                ?? hostEntry.AddressList.FirstOrDefault() 
                                ?? IPAddress.Loopback;
                        }
                        catch
                        {
                            addr = IPAddress.Loopback;
                        }
                    }
                    broker.Publish(null, new AttemptJoin(addr, port));
                    try
                    {
                        var exp = coopExperience;
                        var expType = exp?.GetType();
                        var cfgType = Type.GetType("Coop.Core.Common.Configuration.NetworkConfiguration, Coop.Core", throwOnError: false);
                        var cfg = cfgType != null ? Activator.CreateInstance(cfgType) : null;
                        if (cfg != null)
                        {
                            var propAddr = cfgType.GetProperty("Address");
                            var propPort = cfgType.GetProperty("Port");
                            propAddr?.SetValue(cfg, addr.ToString());
                            propPort?.SetValue(cfg, port);
                        }
                        var startClient = expType?.GetMethod("StartAsClient", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (startClient != null)
                        {
                            Common.Logging.LogManager.GetLogger<CoopMod>().Information("StartAsClient invoked directly with {Address}:{Port}", addr, port);
                            startClient.Invoke(exp, new object[] { cfg });
                            InformationManager.DisplayMessage(new InformationMessage($"[Client] Démarrage client {addr}:{port}"));
                        }
                    }
                    catch { }
                    try { ScreenManager.TryLoseFocus(layer); } catch { }
                    try { ScreenManager.TopScreen.RemoveLayer(layer); } catch { }
                },
                onHost: (port, password) =>
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Hébergement sur port {port}"));
                    try { ScreenManager.TryLoseFocus(layer); } catch { }
                    try { ScreenManager.TopScreen.RemoveLayer(layer); } catch { }
                    Common.Logging.LogManager.GetLogger<CoopMod>().Information("Opening CoopLoadScreen for hosting");
                    var typeNames = new[] {
                        "Coop.UI.LoadGameUI.CoopLoadGameGauntletScreen, GameInterface",
                        "Coop.UI.LoadGameUI.CoopLoadScreen, GameInterface"
                    };
                    ScreenBase screen = null;
                    Type resolvedType = null;
                    foreach (var typeName2 in typeNames)
                    {
                        var t2 = Type.GetType(typeName2, throwOnError: false);
                        if (t2 != null && typeof(ScreenBase).IsAssignableFrom(t2))
                        {
                            resolvedType = t2;
                            try { screen = (ScreenBase)Activator.CreateInstance(t2); } catch (Exception ex) { Common.Logging.LogManager.GetLogger<CoopMod>().Error(ex, "Failed to create instance of {Type}", t2.FullName); }
                            break;
                        }
                    }
                    if (screen == null)
                    {
                        var found = AppDomain.CurrentDomain
                            .GetAssemblies()
                            .SelectMany(a =>
                            {
                                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                            })
                            .FirstOrDefault(foundType =>
                                (foundType.FullName == "Coop.UI.LoadGameUI.CoopLoadScreen" || foundType.FullName == "Coop.UI.LoadGameUI.CoopLoadGameGauntletScreen")
                                && typeof(ScreenBase).IsAssignableFrom(foundType));
                        if (found != null)
                        {
                            resolvedType = found;
                            try { screen = (ScreenBase)Activator.CreateInstance(found); } catch (Exception ex) { Common.Logging.LogManager.GetLogger<CoopMod>().Error(ex, "Failed to create instance of {Type}", found.FullName); }
                        }
                    }
                    if (screen == null)
                    {
                        screen = new HostLoadGauntletScreen();
                        resolvedType = typeof(HostLoadGauntletScreen);
                    }
                    if (screen != null)
                    {
                        try
                        {
                            Common.Logging.LogManager.GetLogger<CoopMod>().Information("Pushing screen {Type}", resolvedType?.FullName ?? "<unknown>");
                            ScreenManager.PushScreen(screen);
                            return;
                        }
                        catch (Exception ex)
                        {
                            Common.Logging.LogManager.GetLogger<CoopMod>().Error(ex, "PushScreen error for {Type}", resolvedType?.FullName ?? "<unknown>");
                        }
                    }
                    // Layer fallback using GameInterface CoopLoadUI
                    try
                    {
                        var vmType = Type.GetType("Coop.UI.LoadGameUI.CoopLoadUI, GameInterface", throwOnError: false);
                        if (vmType != null)
                        {
                            var overlay = new GauntletLayer("CoopHostOverlay", 100, true) { IsFocusLayer = true };
                            var vmInstance = Activator.CreateInstance(vmType);
                            var genericCategory = HotKeyManager.GetCategory("GenericPanelGameKeyCategory");
                            vmType.GetMethod("SetDeleteInputKey")?.Invoke(vmInstance, new object[] { genericCategory.GetHotKey("Delete") });
                            vmType.GetMethod("SetDoneInputKey")?.Invoke(vmInstance, new object[] { genericCategory.GetHotKey("Confirm") });
                            vmType.GetMethod("SetCancelInputKey")?.Invoke(vmInstance, new object[] { genericCategory.GetHotKey("Exit") });

                            try
                            {
                                var spriteData = TaleWorlds.Engine.GauntletUI.UIResourceManager.SpriteData;
                                var spriteCategoriesProp = spriteData.GetType().GetProperty("SpriteCategories");
                                var categories = spriteCategoriesProp?.GetValue(spriteData);
                                var indexer = categories?.GetType().GetProperty("Item");
                                var uiCategory = indexer?.GetValue(categories, new object[] { "ui_saveload" });
                                var resourceContext = TaleWorlds.Engine.GauntletUI.UIResourceManager.ResourceContext;
                                var depotProp = typeof(TaleWorlds.Engine.GauntletUI.UIResourceManager).GetProperty("UIResourceDepot");
                                var depot = depotProp?.GetValue(null);
                                var loadMethod = uiCategory?.GetType().GetMethod("Load", new Type[] { resourceContext.GetType(), depot?.GetType() ?? typeof(object) });
                                loadMethod?.Invoke(uiCategory, new object[] { resourceContext, depot });
                            }
                            catch (Exception ex)
                            {
                                Common.Logging.LogManager.GetLogger<CoopMod>().Warning(ex, "SpriteCategory 'ui_saveload' load skipped");
                            }

                            overlay.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                            overlay.Input.RegisterHotKeyCategory(genericCategory);
                            overlay.LoadMovie("SaveLoadScreen", (ViewModel)vmInstance);
                            ScreenManager.TopScreen.AddLayer(overlay);
                            ScreenManager.TrySetFocus(overlay);
                            Common.Logging.LogManager.GetLogger<CoopMod>().Information("Fallback overlay opened");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.Logging.LogManager.GetLogger<CoopMod>().Error(ex, "Fallback overlay failed");
                    }
                    InformationManager.DisplayMessage(new InformationMessage("Impossible d’ouvrir l’écran de chargement Coop."));
                    Common.Logging.LogManager.GetLogger<CoopMod>().Warning("Failed to resolve or open CoopLoadScreen");
                },
                onGithub: () => { },
                onDiscord: () => { }
            );

            layer.InputRestrictions.SetInputRestrictions();
            try
            {
                layer.LoadMovie("CoopConnectionUIMovie", vm);
            }
            catch
            {
                InformationManager.DisplayMessage(new InformationMessage("Erreur UI: CoopConnectionUIMovie introuvable"));
            }
            ScreenManager.TopScreen.AddLayer(layer);
            ScreenManager.TrySetFocus(layer);
        }

        public override void OnAfterGameInitializationFinished(Game game, object starterObject)
        {
            base.OnAfterGameInitializationFinished(game, starterObject);
        }
    }
        class HostSelectedGameVM : SavedGameVM
        {
        private readonly Action<SavedGameVM> onDelete;
        private readonly Action<SavedGameVM> onSelection;
        public HostSelectedGameVM(SaveGameFileInfo save, bool isSaving, Action<SavedGameVM> onDelete, Action<SavedGameVM> onSelection, Action onCancelLoadSave, Action onDone) :
            base(save, isSaving, onDelete, onSelection, onCancelLoadSave, onDone)
        {
            this.onDelete = onDelete;
            this.onSelection = onSelection;
        }
        public new void ExecuteDelete()
        {
            onDelete(this);
        }
        public new void ExecuteSelection()
        {
            onSelection(this);
        }
        public new void ExecuteSaveLoad()
        {
            if (Game.Current != null)
            {
                ScreenManager.PopScreen();
                GameStateManager.Current.CleanStates(0);
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }
            MessageBroker.Instance.Publish(this, new HostSaveGame(Save.Name));

            InformationManager.DisplayMessage(new InformationMessage($"Chargement de '{Save.Name}'..."));
            CoopMod.TryLoadSaveViaReflection(Save);
        }
    }
    class HostLoadVM : SaveLoadVM
    {
        private new HostSelectedGameVM CurrentSelectedSave;
        public HostLoadVM() : base(false, false)
        {
            GetSavedGames().Clear();
            var saveFiles = MBSaveLoad.GetSaveFiles();
            for (int i = 0; i < saveFiles.Length; i++)
            {
                var item = new HostSelectedGameVM(saveFiles[i], IsSaving, new Action<SavedGameVM>(OnDeleteSavedGame), new Action<SavedGameVM>(OnSaveSelection), new Action(OnCancelLoadSave), new Action(ExecuteDone));
                GetSavedGames().Add(item);
            }
            OnSaveSelection(GetSavedGames().FirstOrDefault());
            RefreshValues();
        }
        private void OnSaveSelection(SavedGameVM saveGame)
        {
            var save = (HostSelectedGameVM)saveGame;
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
        public new void DeleteSelectedSave()
        {
            var currentSelectedSave = CurrentSelectedSave;
            if (currentSelectedSave == null)
            {
                return;
            }
            currentSelectedSave.ExecuteDelete();
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
            string titleText = new TaleWorlds.Localization.TextObject("{=QHV8aeEg}Delete Save", null).ToString();
            string text = new TaleWorlds.Localization.TextObject("{=HH2mZq8J}Are you sure you want to delete this save game?", null).ToString();
            TaleWorlds.Library.InformationManager.ShowInquiry(new TaleWorlds.Library.InquiryData(titleText, text, true, true, new TaleWorlds.Localization.TextObject("{=aeouhelq}Yes", null).ToString(), new TaleWorlds.Localization.TextObject("{=8OkPHu4f}No", null).ToString(), delegate ()
            {
                MBSaveLoad.DeleteSaveGame(savedGame.Save.Name);
                GetSavedGames().Remove(savedGame);
                OnSaveSelection(GetSavedGames().FirstOrDefault());
            }, null, ""), false);
        }
        private TaleWorlds.Library.MBBindingList<SavedGameVM> GetSavedGames() => SaveGroups.FirstOrDefault().SavedGamesList;
    }
    class HostLoadGauntletScreen : ScreenBase
    {
        private GauntletLayer gauntletLayer;
        private HostLoadVM dataSource;
        protected override void OnInitialize()
        {
            base.OnInitialize();
            dataSource = new HostLoadVM();
            dataSource.SetDeleteInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Delete"));
            dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
            dataSource.SetCancelInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Exit"));
            Game.Current?.GameStateManager.RegisterActiveStateDisableRequest(this);
            gauntletLayer = new GauntletLayer("HostLoadLayer", 1, true);
            gauntletLayer.LoadMovie("SaveLoadScreen", dataSource);
            gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            gauntletLayer.IsFocusLayer = true;
            gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            ScreenManager.TrySetFocus(gauntletLayer);
            AddLayer(gauntletLayer);
        }
        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (!dataSource.IsBusyWithAnAction)
            {
                if (gauntletLayer.Input.IsHotKeyReleased("Exit"))
                {
                    dataSource.ExecuteDone();
                    return;
                }
                if (gauntletLayer.Input.IsHotKeyPressed("Confirm") && !gauntletLayer.IsFocusedOnInput())
                {
                    dataSource.ExecuteLoadSave();
                    return;
                }
                if (gauntletLayer.Input.IsHotKeyPressed("Delete") && !gauntletLayer.IsFocusedOnInput())
                {
                    dataSource.DeleteSelectedSave();
                    return;
                }
            }
        }
        protected override void OnFinalize()
        {
            base.OnFinalize();
            Game.Current?.GameStateManager.UnregisterActiveStateDisableRequest(this);
            RemoveLayer(gauntletLayer);
            gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(gauntletLayer);
            gauntletLayer = null;
            dataSource.OnFinalize();
            dataSource = null;
            Utilities.SetForceVsync(false);
        }
    }
}
