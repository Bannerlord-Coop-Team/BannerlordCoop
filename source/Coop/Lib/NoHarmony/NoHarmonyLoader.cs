using System;
using System.Collections.Generic;
using System.IO;
using Medallion.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Coop.Lib.NoHarmony
{
    public abstract class NoHarmonyLoader : MBSubModuleBase
    {
        public enum LogLvl
        {
            Tracking = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        public enum TaskMode
        {
            Add = 0,
            Replace = 1,
            ReplaceOrAdd = 2,
            RemoveAndAdd = 3,
            Remove = 4
        }

        public enum TaskStatus
        {
            Pending,
            Completed,
            Warning,
            Error
        }

        public enum TypeLog
        {
            None = 0,
            Models = 1,
            Behaviors = 2,
            All = 3
        }

        private readonly List<BehaviorDelegate> BehaviorDelegates = new List<BehaviorDelegate>();
        private readonly List<TaskMode> BehaviorModes = new List<TaskMode>();

        private readonly List<ModelDelegate> ModelDelegates = new List<ModelDelegate>();
        private readonly List<TaskMode> ModelModes = new List<TaskMode>();
        private readonly List<TaskStatus> TSBehaviors = new List<TaskStatus>();
        private readonly List<TaskStatus> TSModels = new List<TaskStatus>();
        public string LogDateFormat = "dd/MM/yy HH:mm:ss.fff";
        public string LogFile = "NoHarmony.txt";
        public bool Logging = true;
        public LogLvl MinLogLvl = LogLvl.Info;
        public TypeLog ObjectsToLog = TypeLog.None;
        public bool UseConfFile = false;

        /// <summary>
        ///     Put NoHarmony Initialise code here
        /// </summary>
        public abstract void NoHarmonyInit();

        /// <summary>
        ///     Use add and replace NoHarmony methods here to load your modules;
        /// </summary>
        public abstract void NoHarmonyLoad();
        //End config

        /// <summary>
        ///     NoHarmony will initialize here, don't forget to call base.OnSubModuleLoad() if you override.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            SystemDistributedLock myLock = new SystemDistributedLock("HarmonyGlobalMutex");
            using (myLock.Acquire())
            {
                NoHarmonyInit();
                if (UseConfFile)
                {
                    LoadConf();
                }

                Log(LogLvl.Info, "NoHarmony initialized successfully.");
                NoHarmonyLoad();
                Log(LogLvl.Info,
                    "Pending tasks : " +
                    ModelDelegates.Count +
                    " models, " +
                    BehaviorDelegates.Count +
                    " behaviors.");
            }
        }

        /// <summary>
        ///     Models will be loaded here, don't forget to call base.OnGameStart(game, gameStarterObject) if you override.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="gameStarterObject"></param>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (!(game.GameType is Campaign))
            {
                Log(LogLvl.Error, "Game is not a campaign.");
                return;
            }

            if (ModelDelegates.Count != ModelModes.Count)
            {
                Log(
                    LogLvl.Error,
                    "Task lost during preprocessing, skiping all models modification.");
                return;
            }

            for (int index = 0; index < ModelDelegates.Count; ++index)
            {
                TaskStatus result =
                    ModelDelegates[index].Invoke(gameStarterObject, ModelModes[index]);
                TSModels.Add(result);
            }
        }

        /// <summary>
        ///     Behaviors operations wille be done here, don't forget to call.OnGameInitializationFinished(game) base if you
        ///     override.
        /// </summary>
        /// <param name="game"></param>
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            if (!(game.GameType is Campaign campaign))
            {
                Log(LogLvl.Error, "Game is not a campaign.");
                return;
            }

            if (BehaviorDelegates.Count != BehaviorModes.Count)
            {
                Log(
                    LogLvl.Error,
                    "Task lost during preprocessing, skiping all behaviors modification.");
                return;
            }

            for (int index = 0; index < BehaviorDelegates.Count; ++index)
            {
                TaskStatus result = BehaviorDelegates[index].Invoke(campaign, BehaviorModes[index]);
                TSBehaviors.Add(result);
            }
        }

        /// <summary>
        ///     Use this method in your NoHarmonyLoad() method to add new models to the game.
        /// </summary>
        /// <typeparam name="AddType">Model type to add</typeparam>
        public void AddModel<AddType>()
            where AddType : GameModel, new()
        {
            ModelDelegates.Add(NHLModel<AddType, GameModel>);
            ModelModes.Add(TaskMode.Add);
        }

        /// <summary>
        ///     Use this method in your NoHarmonyLoad() method to replace a specified model by a new specified one.
        /// </summary>
        /// <typeparam name="AddType">Model type to add</typeparam>
        /// <typeparam name="RemoveType">Model type to be replaced</typeparam>
        /// <param name="m">Not required. The replace mode to use. (Do not pick add or remove)</param>
        public void ReplaceModel<AddType, RemoveType>(TaskMode m = TaskMode.Replace)
            where AddType : GameModel, new()
            where RemoveType : GameModel
        {
            if (TaskMode.Add == m || m == TaskMode.Remove)
            {
                return;
            }

            ModelDelegates.Add(NHLModel<AddType, RemoveType>);
            ModelModes.Add(m);
        }

        /// <summary>
        ///     Use this method in your NoHarmonyLoad() method to remove a specified model. (Dangerous)
        /// </summary>
        /// <typeparam name="RemoveType">Model type to remove</typeparam>
        public void RemoveModel<RemoveType>()
            where RemoveType : GameModel
        {
            ModelDelegates.Add(NHLModel<DummyModel, RemoveType>);
            ModelModes.Add(TaskMode.Remove);
        }

        /// <summary>
        ///     Use this method in your NoHarmonyLoad() method to add new behavior to the game.
        /// </summary>
        /// <typeparam name="AddType">Behavior type to add</typeparam>
        public void AddBehavior<AddType>()
            where AddType : CampaignBehaviorBase, new()
        {
            BehaviorDelegates.Add(NHLBehavior<AddType, CampaignBehaviorBase>);
            BehaviorModes.Add(TaskMode.Add);
        }

        /// <summary>
        ///     Use this method in your NoHarmonyLoad() method to replace a specified behavior by a new specified one.
        /// </summary>
        /// <typeparam name="AddType">Behavior type to add</typeparam>
        /// <typeparam name="RemoveType">Behavior type to remove</typeparam>
        /// <param name="m">Not required. The replace mode to use. (Do not pick add or remove)</param>
        public void ReplaceBehavior<AddType, RemoveType>(TaskMode m = TaskMode.Replace)
            where AddType : CampaignBehaviorBase, new()
            where RemoveType : CampaignBehaviorBase
        {
            BehaviorDelegates.Add(NHLBehavior<AddType, RemoveType>);
            BehaviorModes.Add(m);
        }

        private TaskStatus NHLModel<AddType, RemoveType>(IGameStarter gameI, TaskMode mode)
            where RemoveType : GameModel
            where AddType : GameModel, new()
        {
            IList<GameModel> models = gameI.Models as IList<GameModel>;
            TaskStatus st = TaskStatus.Pending;
            int rm = 0;

            for (int index = 0; index < models.Count; ++index)
            {
                if (mode != TaskMode.Remove && models[index] is AddType)
                {
                    Log(LogLvl.Warning, typeof(AddType) + " already installed, skipping.");
                    if (mode == TaskMode.RemoveAndAdd)
                    {
                        st = TaskStatus.Warning;
                    }
                    else
                    {
                        return TaskStatus.Warning;
                    }
                }

                if (models[index] is RemoveType)
                {
                    if (mode == TaskMode.Replace || mode == TaskMode.ReplaceOrAdd)
                    {
                        models[index] = new AddType();
                        Log(
                            LogLvl.Info,
                            typeof(RemoveType) +
                            " found and replaced with " +
                            typeof(AddType) +
                            ".");
                        return TaskStatus.Completed;
                    }

                    if (mode == TaskMode.Remove || mode == TaskMode.RemoveAndAdd)
                    {
                        models.RemoveAt(index); // C# rearrange the for loop on it's own. 
                        rm++;
                        index--;
                    }
                }
            }

            if (mode != TaskMode.Replace && mode != TaskMode.Remove && st == TaskStatus.Pending)
            {
                gameI.AddModel(new AddType());
                Log(LogLvl.Info, typeof(AddType) + " added.");
            }

            if (mode == TaskMode.RemoveAndAdd)
            {
                if (rm == 0)
                {
                    Log(LogLvl.Warning, typeof(RemoveType) + " not found.");
                    st = TaskStatus.Warning;
                }

                if (st == TaskStatus.Pending)
                {
                    st = TaskStatus.Completed;
                }

                return st;
            }

            if (mode == TaskMode.Remove && rm == 0)
            {
                Log(LogLvl.Warning, typeof(RemoveType) + " not found.");
                return TaskStatus.Warning;
            }

            return TaskStatus.Completed;
        }

        private TaskStatus NHLBehavior<AddType, RemoveType>(Campaign campaign, TaskMode mode)
            where RemoveType : CampaignBehaviorBase
            where AddType : CampaignBehaviorBase, new()
        {
            CampaignBehaviorManager cbm =
                (CampaignBehaviorManager)campaign.CampaignBehaviorManager;
            if (mode != TaskMode.Add)
            {
                RemoveType cgb = campaign.GetCampaignBehavior<RemoveType>();
                CampaignEventDispatcher.Instance.RemoveListeners(cgb);
                cbm.RemoveBehavior<RemoveType>();
            }

            if (mode != TaskMode.Remove)
            {
                cbm.AddBehavior(new AddType());
            }

            return TaskStatus.Completed;
        }

        /// <summary>
        ///     Add a message to NoHarmony log file
        /// </summary>
        /// <param name="mLvl">Message logging level</param>
        /// <param name="message">Message core</param>
        public void Log(LogLvl mLvl, string message)
        {
            if (mLvl.CompareTo(MinLogLvl) < 0 || !Logging)
            {
                return;
            }

            switch (mLvl)
            {
                case LogLvl.Error:
                    message = "!![Error] " + message + " !!";
                    break;
                case LogLvl.Warning:
                    message = "![Warn] " + message;
                    break;
                case LogLvl.Info:
                    message = "[Info] " + message;
                    break;
                case LogLvl.Tracking:
                    message = "[Track] " + message;
                    break;
            }
            try
            {
                using (StreamWriter sw = new StreamWriter(LogFile, true))
                {
                    sw.WriteLine(DateTime.Now.ToString(LogDateFormat) + " > " + message);
                }
            }
            catch (IOException) { }
        }

        public void LoadConf()
        {
        }

        // NoHarmony core features past this point
        private delegate TaskStatus ModelDelegate(IGameStarter gameStarter, TaskMode mode);

        private delegate TaskStatus BehaviorDelegate(Campaign campaign, TaskMode mode);
    }

    public sealed class DummyModel : GameModel
    {
    } // used to replace null, should never end in the game.

    public sealed class
        DummyBehavior : CampaignBehaviorBase // used to replace null, should never end in the game.
    {
        public override void RegisterEvents()
        {
            throw new NotImplementedException();
        }

        public override void SyncData(IDataStore dataStore)
        {
            throw new NotImplementedException();
        }
    }
}
