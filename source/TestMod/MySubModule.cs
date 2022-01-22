using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.SaveSystem;
using TaleWorlds.InputSystem;
using System.IO;
using TaleWorlds.Library;
using System.Linq;
using TaleWorlds.Engine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.MountAndBlade.Diamond;
using HarmonyLib;
using NetworkMessages.FromServer;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using SandBox;

namespace CoopTestMod
{
    struct EquipmentHitPoint
    {
        public bool IsShield {get; private set;} 
        public short HitPoint { get; private set; }
        public EquipmentIndex Index { get; private set; }

        public EquipmentHitPoint(bool _isShield, short _hitPoint, EquipmentIndex _index) 
        {
            IsShield=_isShield;
            HitPoint = _hitPoint;
            Index = _index;
        }
    }


    //[Serializable]
    //public class AgentSerilizer : CustomSerializer
    //{
    //    public AgentSerilizer(Agent agent) : base(agent)
    //    {

    //    }
    //    Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
    //    public override object Deserialize()
    //    {



    //        foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
    //        {
    //            entry.Key.SetValue(newClan, entry.Value.Deserialize());
    //        }
    //        base.Deserialize(newClan);
    //    }

    //    public override void ResolveReferenceGuids()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}



    public class MySubModule : MBSubModuleBase
    {

        public class MessageParser
        {
            private int bitExtracted(int number, int k, int p)
            {
                return (((1 << k) - 1) & (number >> (p - 1)));
            }

            public void parseMessage(byte[] bytes, Agent otherAgent, Agent playerAgent, ref uint currentId, ref float otherAgentHealth)
            {
                float x = BitConverter.ToSingle(bytes, 0);
                float y = BitConverter.ToSingle(bytes, 4);
                float z = BitConverter.ToSingle(bytes, 8);
                uint movementFlag = BitConverter.ToUInt32(bytes, 12);
                uint eventFlag = BitConverter.ToUInt32(bytes, 16);
                float moveX = BitConverter.ToSingle(bytes, 20);
                float moveY = BitConverter.ToSingle(bytes, 24);
                float inputVectorX = BitConverter.ToSingle(bytes, 28);
                float inputVectorY = BitConverter.ToSingle(bytes, 32);
                int cacheIndex1 = BitConverter.ToInt32(bytes, 36);
                float progress1 = BitConverter.ToSingle(bytes, 40);
                AnimFlags flags1 = (AnimFlags)BitConverter.ToUInt64(bytes, 44);
                int cacheIndex2 = BitConverter.ToInt32(bytes, 52);
                float progress2 = BitConverter.ToSingle(bytes, 56);
                AnimFlags flags2 = (AnimFlags)BitConverter.ToUInt64(bytes, 60);
                //int cacheIndex3 = BitConverter.ToInt32(bytes, 68);
                //float progress3 = BitConverter.ToSingle(bytes, 72);
                //AnimFlags flags3 = (AnimFlags)BitConverter.ToUInt64(bytes, 76);
                float lookDirectionX = BitConverter.ToSingle(bytes, 68);
                float lookDirectionY = BitConverter.ToSingle(bytes, 72);
                float lookDirectionZ = BitConverter.ToSingle(bytes, 76);
                float health = BitConverter.ToSingle(bytes, 80);
                uint packetId = BitConverter.ToUInt32(bytes, 84);
                Agent.ActionCodeType ch0 = (Agent.ActionCodeType)BitConverter.ToInt32(bytes, 88);
                Agent.ActionCodeType ch1 = (Agent.ActionCodeType)BitConverter.ToInt32(bytes, 92);
                bool crouchMode = BitConverter.ToBoolean(bytes, 96);

                float mInputVectorX = BitConverter.ToSingle(bytes, 97);
                float mInputVectorY = BitConverter.ToSingle(bytes, 101);
                AnimFlags mFlags2 = (AnimFlags)BitConverter.ToUInt64(bytes, 105);
                float mProgress2 = BitConverter.ToSingle(bytes, 113);
                int mCacheIndex2 = BitConverter.ToInt32(bytes, 117);
                float playerAgentHealth = BitConverter.ToSingle(bytes, 121);

                EquipmentIndex wieldedMeleeWeaponIndex = (EquipmentIndex)BitConverter.ToInt32(bytes, 125);
                EquipmentIndex wieldedOffHandWeapon = (EquipmentIndex)BitConverter.ToInt32(bytes, 129);
                EquipmentHitPoint[] HitPoints = new EquipmentHitPoint[4];
                if (playerAgent != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        EquipmentIndex Index = (EquipmentIndex)BitConverter.ToInt32(bytes, 133 + i * 4 + i * 2);
                        short ShieldHealth = BitConverter.ToInt16(bytes, 137 + i * 4 + i * 2);
                        HitPoints[i] = new EquipmentHitPoint(playerAgent.Equipment[Index].IsShield(), ShieldHealth, Index);
                    }
                }
                //Next StartIndex: 157
                bool EnemyAgentShot = BitConverter.ToBoolean(bytes, 157);
                CreateMissile message = null;
                if (EnemyAgentShot)
                    message = IOCreateMissile.Read(bytes,otherAgent,158);


                //int damageTaken = BitConverter.ToInt32(bytes, 121);


                //InformationManager.DisplayMessage(new InformationMessage("CH0 Action: " + ch0));
                //InformationManager.DisplayMessage(new InformationMessage("CH1 Action: " + ch1));
                //float targetPositionX = BitConverter.ToSingle(bytes, 80);
                //float targetPositionY = BitConverter.ToSingle(bytes, 84);
                //float targetDirectionX = BitConverter.ToSingle(bytes, 88);
                //float targetDirectionY = BitConverter.ToSingle(bytes, 92);
                //float targetDirectionZ = BitConverter.ToSingle(bytes, 96);

                //otherAgent.UpdateSyncHealthToAllClients(true);
                Vec3 pos = new Vec3(x, y, z);
                //Vec2 targetPosition = new Vec2(targetPositionX, targetPositionY);
                //Vec3 targetDirection = new Vec3(targetDirectionX, targetDirectionY, targetDirectionZ);
                //Agent.UsageDirection direction = Agent.MovementFlagToDirection((Agent.MovementControlFlag)movementFlag);



                if (Mission.Current != null && otherAgent != null)
                {
                    //otherAgent.TeleportToPosition(pos);
                    //InformationManager.DisplayMessage(new InformationMessage("Received ID: " + currentId.ToString()));
                    if (packetId < currentId)
                    {
                        return;
                    }
                    else
                    {
                        currentId = packetId;
                    }
                    //InformationManager.DisplayMessage(new InformationMessage("Processed ID: " + currentId.ToString()));
                    if (playerAgentHealth < playerAgent.Health)
                    {
                        Blow b = new Blow(otherAgent.Index);
                        b.InflictedDamage = (int)(playerAgent.Health - playerAgentHealth);
                        playerAgent.RegisterBlow(b);

                    }
                    //We are going through the EquipmentSlots and change the HitPoint if it's damaged and there is a shield in the slot.
                    foreach (EquipmentHitPoint HitPoint in HitPoints)
                        if (HitPoint.IsShield && playerAgent.Equipment[HitPoint.Index].HitPoints > HitPoint.HitPoint)
                        {
                            playerAgent.ChangeWeaponHitPoints(HitPoint.Index, HitPoint.HitPoint);
                            if (HitPoint.HitPoint == 0)
                                playerAgent.RemoveEquippedWeapon(HitPoint.Index);
                        }

                    if (message != null)
                    {
                        Vec3 velocity = message.Direction * message.Speed;
                        OnAgentShootMissileMethod.Invoke(Mission.Current,new object[] { message.Agent, message.WeaponIndex, message.Position, velocity, message.Orientation, message.HasRigidBody, message.IsPrimaryWeaponShot, message.MissileIndex });
                    }
                    
                    
                    //InformationManager.DisplayMessage(new InformationMessage("OffHandWeapon: " + wieldedOffHandWeapon.ToString()));


                    //if (wieldedWeapon != Convert.ToInt32(otherAgent.WieldedWeapon.RawDataForNetwork))
                    //{

                    //    otherAgent.WieldNextWeapon(Agent.HandIndex.MainHand);

                    //}
                    //InformationManager.DisplayMessage(new InformationMessage("wMWI: " + wieldedMeleeWeaponIndex.ToString() + " oA CUI: " + otherAgent.WieldedWeapon.CurrentUsageIndex));

                    if (wieldedMeleeWeaponIndex != otherAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand))
                    {
                        otherAgent.WieldNextWeapon(Agent.HandIndex.MainHand);
                    }

                    if (wieldedOffHandWeapon != otherAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand))
                    {
                        otherAgent.WieldNextWeapon(Agent.HandIndex.OffHand);
                    }


                    if (otherAgent.Health <= 0)
                    {
                        return;
                    }



                    if (otherAgent.GetPathDistanceToPoint(ref pos) > 0.3f)
                    {
                        otherAgent.TeleportToPosition(pos);
                    }

                    //otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;
                    //otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;

                    //InformationManager.DisplayMessage(new InformationMessage(ch1.ToString()));


                    otherAgent.EventControlFlags = 0U;
                    if (crouchMode)
                    {
                        otherAgent.EventControlFlags |= Agent.EventControlFlag.Crouch;
                    }
                    else
                    {
                        otherAgent.EventControlFlags |= Agent.EventControlFlag.Stand;
                    }


                    otherAgent.LookDirection = new Vec3(lookDirectionX, lookDirectionY, lookDirectionZ);
                    otherAgent.MovementInputVector = new Vec2(inputVectorX, inputVectorY);

                    if (eventFlag == 1u)
                    {
                        otherAgent.EventControlFlags |= Agent.EventControlFlag.Dismount;
                    }
                    if (eventFlag == 2u)
                    {
                        otherAgent.EventControlFlags |= Agent.EventControlFlag.Mount;
                    }




                    if (otherAgent.HasMount)
                    {
                        otherAgent.MountAgent.SetMovementDirection(new Vec2(mInputVectorX, mInputVectorY));

                        //Currently not doing anything afaik
                        //if (otherAgent.MountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || otherAgent.MountAgent.GetCurrentAction(1).Index != mCacheIndex2)
                        //{
                        //    string mActionName2 = MBAnimation.GetActionNameWithCode(mCacheIndex2);
                        //    otherAgent.MountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: (ulong)mFlags2, startProgress: mProgress2);
                        //}
                        //else
                        //{
                        //    otherAgent.MountAgent.SetCurrentActionProgress(1, mProgress2);
                        //}
                    }


                    if (otherAgent.GetCurrentAction(0) == ActionIndexCache.act_none || otherAgent.GetCurrentAction(0).Index != cacheIndex1)
                    {
                        string actionName1 = MBAnimation.GetActionNameWithCode(cacheIndex1);
                        otherAgent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (ulong)flags1, startProgress: progress1);

                    }
                    else
                    {
                        otherAgent.SetCurrentActionProgress(0, progress1);
                    }
                    otherAgent.MovementFlags = 0U;

                    if ((int)ch1 >= (int)Agent.ActionCodeType.DefendAllBegin && (int)ch1 <= (int)Agent.ActionCodeType.DefendAllEnd)

                    {
                        otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;
                        return;
                    }



                    //// we either don't have an action so set it to the new one or the receive action is different than our current action

                    if (ch1 != Agent.ActionCodeType.BlockedMelee)
                    {
                        if (otherAgent.GetCurrentAction(1) == ActionIndexCache.act_none || otherAgent.GetCurrentAction(1).Index != cacheIndex2)
                        {
                            string actionName2 = MBAnimation.GetActionNameWithCode(cacheIndex2);
                            otherAgent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (ulong)flags2, startProgress: progress2);

                        }
                        else
                        {
                            otherAgent.SetCurrentActionProgress(1, progress2);
                        }
                    }
                    else
                    {

                        otherAgent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true, startProgress: 100);
                    }

                    //otherAgent.MovementFlags = 0U;
                    //otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;

                    //InformationManager.DisplayMessage(new InformationMessage(Agent.Main.Position + ""));

                    //if (health != otherAgent.Health)
                    //{
                    //    //InformationManager.DisplayMessage(new InformationMessage("otherAgent.Health: " + otherAgent.Health));
                    //    //InformationManager.DisplayMessage(new InformationMessage("damageTaken: " + damageTaken));
                    //    //InformationManager.DisplayMessage(new InformationMessage("health: " + health));

                    //    if (otherAgent.Health < 0)
                    //    {
                    //        otherAgent.MakeDead(true, otherAgent.GetCurrentAction(1)); //Which action do we require or what does it do?
                    //    }
                    //}


                    //if (eventFlag != 0)
                    //{
                    //    otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;

                    //}

                    //if (otherAgent.GetCurrentAction(1) != ActionIndexCache.act_none && otherAgent.CurrentGuardMode == Agent.GuardMode.None)
                    //{
                    //    otherAgent.EventControlFlags = Agent.EventControlFlag.Stand;
                    //}


                    /*
                    if (otherAgent == Agent.ActionCodeType.)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(otherAgent.CrouchMode.ToString()));

                        otherAgent.EventControlFlags = Agent.EventControlFlag.Stand;
                    } */

                    // if (otherAgent.GetCurrentAction(1) != ActionIndexCache.act_none)
                    // {
                    //    InformationManager.DisplayMessage(new InformationMessage(otherAgent.GetCurrentActionType(1).ToString()));
                    // }


                    //otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;
                    //otherAgent.SetMovementDirection(new Vec2(moveX, moveY));
                    //otherAgent.AttackDirectionToMovementFlag(direction);
                    //otherAgent.DefendDirectionToMovementFlag(direction);

                    // otherAgent.EnforceShieldUsage(direction);
                    //InformationManager.DisplayMessage(new InformationMessage("Received: " + ((Agent.EventControlFlag)eventFlag).ToString()));



                    //InformationManager.DisplayMessage(new InformationMessage("Received : X: " +  lookDirectionX + " Y: " + lookDirectionY + " | Z: " + lookDirectionZ));



                    //InformationManager.DisplayMessage(new InformationMessage("Receiving: " + ((Agent.EventControlFlag)eventFlag).ToString()));

                    //otherAgent.SetTargetPositionAndDirection(targetPosition, targetDirection);


                    //otherAgent.SetCurrentActionProgress(1, progress2);

                    //string actionName3 = MBAnimation.GetActionNameWithCode(cacheIndex3);
                    //otherAgent.SetActionChannel(2, ActionIndexCache.Create(actionName3), additionalFlags: (ulong)flags3, startProgress: progress3);
                    //otherAgent.SetCurrentActionProgress(2, progress3);




                }
            }
        }

        private Agent _otherAgent;
        private Agent _player;

        private Socket sender;
        private Socket receiver;
        private UIntPtr playerPtr;
        private UIntPtr otherAgentPtr;
        private const int bufSize = 1024;
        private static MethodInfo OnAgentShootMissileMethod = typeof(Mission).GetMethod("OnAgentShootMissile",BindingFlags.NonPublic|BindingFlags.Instance);

        Func<UIntPtr, Vec3> getPosition;
        PositionRefDelegate setPosition;
        List<MatrixFrame> _initialSpawnFrames;
        MatrixFrame randomElement2;
        float t;
        AgentBuildData agentBuildData;
        AgentBuildData agentBuildData2;
        bool subModuleLoaded = false;
        bool battleLoaded = false;
        EndPoint epFrom;
        EndPoint epTo;
        bool isServer = false;
        uint packetId = 1;
        uint currentId = 0;

        Vec2 mInputVector;
        AnimFlags mFlags1;
        float mProgress1;
        ActionIndexCache mCache1;

        private float otherAgentHealth = 0;

        public static bool MyAgentShot = false;

        public static object MyAgent { get; private set; }

        // custom delegate is needed since SetPosition uses a ref Vec3
        delegate void PositionRefDelegate(UIntPtr agentPtr, ref Vec3 position);

        // utility to keep trying to connect to server if it fails
        public bool ClientConnect(IPEndPoint remoteEP)
        {
            try
            {
                sender.Connect(remoteEP);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // thread to allow connections and handle data sent by the client

        public void initSockets(string ipAddress, int sendPort, int recvPort)
        {
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            receiver.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            receiver.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), recvPort));

            sender.Connect(IPAddress.Parse(ipAddress), sendPort);
        }


        public void StartServer()
        {
            initSockets("127.0.0.1", 14905, 14906);
            MessageParser messageParser = new MessageParser();
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            epTo = new IPEndPoint(ipAddress, 14905);
            epFrom = new IPEndPoint(ipAddress, 14906);


            try
            {



                // Incoming data from the client.

                byte[] bytes = null;

                while (true)
                {
                    try
                    {
                        bytes = new byte[1024];
                        int bytesRec = receiver.ReceiveFrom(bytes, ref epFrom);
                        messageParser.parseMessage(bytes, _otherAgent, _player, ref currentId, ref otherAgentHealth);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("wouterror.txt", ex.Message);
                    }




                }





            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                sender.Close();
                receiver.Close();
            }
        }

        // thread to receive the data as the client
        public void StartClient()
        {
            byte[] bytes = null;
            MessageParser messageParser = new MessageParser();
            initSockets("127.0.0.1", 14906, 14905);
            IPAddress ipAddress = System.Net.IPAddress.Parse("127.0.0.1");
            epTo = new IPEndPoint(ipAddress, 14906);
            epFrom = new IPEndPoint(ipAddress, 14905);
            try
            {
                while (true)
                {
                    bytes = new byte[1024];
                    int bytesRec = receiver.ReceiveFrom(bytes, ref epFrom);
                    messageParser.parseMessage(bytes, _otherAgent, _player, ref currentId, ref otherAgentHealth);
                }

            }
            catch (Exception e)
            {

            }
            finally
            {
                sender.Close();
                receiver.Close();
            }
        }


        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop").PatchAll();

            //skip intro
            FieldInfo splashScreen = TaleWorlds.MountAndBlade.Module.CurrentModule.GetType().GetField("_splashScreenPlayed", BindingFlags.Instance | BindingFlags.NonPublic);
            splashScreen.SetValue(TaleWorlds.MountAndBlade.Module.CurrentModule, true);


            // pass /server or /client to start as either or
            Thread thread = null;
            string[] array = Utilities.GetFullCommandLineString().Split(' ');
            foreach (string argument in array)
            {
                if (argument.ToLower() == "/server")
                {
                    isServer = true;
                    thread = new Thread(StartServer);
                }
                else if (argument.ToLower() == "/client")
                {
                    thread = new Thread(StartClient);
                }
            }
            thread.IsBackground = true;
            thread.Start();




        }


        private void StartArenaFight()
        {
            //reset teams if any exists

            Mission.Current.ResetMission();

            //
            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            //players is defender team
            Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


            //find areas of spawn

            _initialSpawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                   select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < _initialSpawnFrames.Count; i++)
            {
                MatrixFrame value = _initialSpawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                _initialSpawnFrames[i] = value;
            }
            // get a random spawn point
            MatrixFrame randomElement = _initialSpawnFrames.GetRandomElement();
            //remove the point so no overlap
            _initialSpawnFrames.Remove(randomElement);
            //find another spawn point
            randomElement2 = randomElement;


            // spawn an instance of the player (controlled by default)
            _player = SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, true);
            MyAgent = _player;


            //spawn another instance of the player, uncontroller (this should get synced when someone joins)
            _otherAgent = SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement2, false);


            otherAgentHealth = _otherAgent.Health;


            // Our agent's pointer; set it to 0 first
            playerPtr = UIntPtr.Zero;


            // other agent's pointer
            otherAgentPtr = (UIntPtr)_otherAgent.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_otherAgent);


            // Find out agent's pointer from our agent instance
            playerPtr = (UIntPtr)_player.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_player);


            // set the weapons to the available weapons
            _player.WieldInitialWeapons();
            _otherAgent.WieldInitialWeapons();


            //// From MBAPI, get the private interface IMBAgent
            FieldInfo IMBAgentField = typeof(MBAPI).GetField("IMBAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            // get the set and get method of position
            MethodInfo getPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("GetPosition");
            MethodInfo setPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("SetPosition");


            // set the delegates to the method pointers. In case Agent class isn't enough we can invoke IMAgent directly.
            getPosition = (Func<UIntPtr, Vec3>)Delegate.CreateDelegate
                (typeof(Func<UIntPtr, Vec3>), IMBAgentField.GetValue(null), getPositionMethod);

            setPosition = (PositionRefDelegate)Delegate.CreateDelegate(typeof(PositionRefDelegate), IMBAgentField.GetValue(null), setPositionMethod);
        }

        // ripped straight out of arena spawns
        private Agent SpawnArenaAgent(CharacterObject character, MatrixFrame frame, bool isMain)
        {
            agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData2 = agentBuildData.Team(isMain ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);                             //this spawns an archer
            //Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(CharacterObject.Find("conspiracy_guardian").Equipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);    //this spawns a spearman
            agent.FadeIn();
            if (isMain)
            {
                agent.Controller = Agent.ControllerType.Player;

            }
            else
            {

                agent.Controller = Agent.ControllerType.None;

            }

            return agent;
        }

        [HarmonyPatch(typeof(Mission), "OnAgentHit")]
        public class MissionOnAgentHitPatch
        {
            private static int damageDone;
            public static int DamageDone
            {
                get
                {
                    return damageDone;
                }
            }

            public static void Postfix(int damage)
            {
                InformationManager.DisplayMessage(new InformationMessage(damage.ToString()));
                damageDone = damage;
            }
        }

        [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CalculateEffectiveMissileSpeed")]
        public class CalculateEffectiveMissileSpeedPatch
        {
            public static void Postfix(float __result)
            {
                OnAgentShootMissilePatch.num = __result;
            }
        }

        [HarmonyPatch(typeof(Mission), "AddMissileAux")]
        public class AddMissileAuxPatch
        {
            public static void Postfix(int __result,Vec3 direction)
            {
                OnAgentShootMissilePatch.num3 = __result;
                OnAgentShootMissilePatch.direction= direction;
            }
        }

        [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
        public class OnAgentShootMissilePatch
        {
            public static CreateMissile Message;
            public static int num3;
            public static Vec3 direction;
            public static float num;
            public static void Postfix(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity,
                Mat3 orientation, bool hasRigidBody, bool isPrimaryWeaponShot, int forcedMissileIndex)
            {
                if (shooterAgent.Equals(MySubModule.MyAgent))
                {
                    MySubModule.MyAgentShot = true;
                    Message= new CreateMissile(num3, shooterAgent, weaponIndex, MissionWeapon.Invalid, position, direction, num, orientation, hasRigidBody, null, isPrimaryWeaponShot);
                }
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            if (!subModuleLoaded && Module.CurrentModule.LoadingFinished)
            {
                // sub module is loaded. Isnt there a proper callback for this?
                subModuleLoaded = true;

                //Get all the save sames
                SaveGameFileInfo[] games = MBSaveLoad.GetSaveFiles();


                // just load the first one. 
                LoadResult result = SaveManager.Load(games[0].Name, new AsyncFileSaveDriver(), true);

                // Create our own game manager. This will help us override the OnLoaded callback and load the town
                CustomSandboxGame manager = new CustomSandboxGame(result);

                //start it
                MBGameManager.StartNewGame(manager);
            }

            // Press slash next to spawn in the arena
            if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            {
                // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
                battleLoaded = true;
                StartArenaFight();
            }


            if (Input.IsKeyReleased(InputKey.Slash))
            {

                //FieldInfo IMBNetwork = typeof(MBAPI).GetField("IMBNetwork", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);


                if (isServer)
                {
                    InformationManager.DisplayMessage(new InformationMessage("I am Server"));
                    try
                    {
                        GameNetwork.Initialize(new GameNetworkHandler());
                        GameNetwork.InitializeCompressionInfos();
                        FieldInfo IMBAgentField = typeof(GameNetwork).GetField("IMBAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        MethodInfo method2 = typeof(GameNetwork).GetMethod("PreStartMultiplayerOnServer", BindingFlags.Static | BindingFlags.NonPublic);
                        MethodInfo method = typeof(GameNetwork).GetMethod("InitializeServerSide", BindingFlags.Static | BindingFlags.NonPublic);

                        if (method != null)
                        {
                            //MBCommon.CurrentGameType = (GameNetwork.IsDedicatedServer ? MBCommon.GameType.MultiServer : MBCommon.GameType.MultiClientServer);
                            GameNetwork.ClientPeerIndex = -1;


                            method.Invoke(null, new object[] { 15801 });
                            //GameNetwork.StartMultiplayerOnClient("127.0.0.1", 15801, 1, 1);
                            //BannerlordNetwork.StartMultiplayerLobbyMission(LobbyMissionType.Custom);
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage("Not found!"));
                        }

                        //GameNetwork.StartMultiplayerOnServer(15801);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("wouterror.txt", ex.Message);
                    }

                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("I am client"));
                    //MethodInfo initServer = IMBNetwork.GetValue(null).GetType().GetMethod("InitializeClientSide");
                    //initServer.Invoke(IMBNetwork.GetValue(null), new object[] { "127.0.0.1", 14890, 0, 0 });
                    GameNetwork.Initialize(new GameNetworkHandler());
                    GameNetwork.InitializeCompressionInfos();
                    GameNetwork.StartMultiplayerOnClient("127.0.0.1", 15801, 1, 1);
                    BannerlordNetwork.StartMultiplayerLobbyMission(LobbyMissionType.Custom);



                }


            }

            if (Input.IsKeyReleased(InputKey.Numpad5))
            {

                Blow b = new Blow(_otherAgent.Index);
                b.InflictedDamage = 20;
                //_player.Health = 0;
                _player.RegisterBlow(b);



            }
            if (Input.IsKeyReleased(InputKey.Numpad6))
            {

                Blow b = new Blow(_player.Index);
                b.InflictedDamage = 20;
                //_player.Health = 0;
                _otherAgent.RegisterBlow(b);
            }
            if (Input.IsReleased(InputKey.Numpad1))
            {
                //_player.SetAIBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.AttackEntityMelee, 1f, 1f, 1f, 1f, 1f);
                // _player.SetActionChannel(1, ActionIndexCache.Create("act_defend_shield_up_1h_passive_down"), ignorePriority: true, 0);
                InformationManager.DisplayMessage(new InformationMessage("Crouching?"));
                _otherAgent.EventControlFlags = Agent.EventControlFlag.Crouch;
                //InformationManager.DisplayMessage(new InformationMessage(_otherAgent.EventControlFlags.ToString()));
            }

            if (Input.IsReleased(InputKey.Numpad2))
            {
                //_player.SetAIBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.AttackEntityMelee, 1f, 1f, 1f, 1f, 1f);
                // _player.SetActionChannel(1, ActionIndexCache.Create("act_defend_shield_up_1h_passive_down"), ignorePriority: true, 0);
                InformationManager.DisplayMessage(new InformationMessage("Crouching?"));
                _otherAgent.EventControlFlags = Agent.EventControlFlag.Stand;
                // InformationManager.DisplayMessage(new InformationMessage(_otherAgent.EventControlFlags.ToString()));
            }

            if (Input.IsReleased(InputKey.Numpad9))
            {
                StartArenaFight();
            }

            if (Input.IsReleased(InputKey.Numpad7))
            {
                _otherAgent.WieldNextWeapon(Agent.HandIndex.MainHand);
            }

            if (Input.IsReleased(InputKey.Numpad3))
            {
                _otherAgent.WieldNextWeapon(Agent.HandIndex.OffHand);
            }
            if (Input.IsReleased(InputKey.Numpad4))
            {
                InformationManager.DisplayMessage(new InformationMessage(_player.WieldedOffhandWeapon.HitPoints.ToString()));
                InformationManager.DisplayMessage(new InformationMessage(_otherAgent.WieldedOffhandWeapon.HitPoints.ToString()));
            }

            if (Input.IsReleased(InputKey.CapsLock))
            {
                // _otherAgent.WieldNextWeapon(new Agent.HandIndex());


                foreach (CharacterObject co in CharacterObject.All)
                    InformationManager.DisplayMessage(new InformationMessage(co.Name.ToString() + " " + co.StringId.ToString()));

            }

            // Mission is loaded
            if (Mission.Current != null && playerPtr != UIntPtr.Zero)
            {
                // every 0.1 tick send an update to other endpoint
                if (t + 0.015 > Time.ApplicationTime)
                {
                    return;
                }
                // update time
                t = Time.ApplicationTime;

                // create a memory stream
                MemoryStream stream = new MemoryStream();

                // get all the values needed to sync character (there is more for actions, weapon switching, etc).
                Vec3 myPos = getPosition(playerPtr);
                uint movementFlag = (uint)_player.MovementFlags;
                uint eventFlag = (uint)_player.EventControlFlags;
                Vec2 movementDirection = _player.GetMovementDirection();
                Vec2 inputVector = _player.MovementInputVector;
                ActionIndexCache cache1 = ActionIndexCache.act_none;
                float progress1 = 0f;
                AnimFlags flags1 = 0;
                ActionIndexCache cache2 = ActionIndexCache.act_none;
                float progress2 = 0f;
                AnimFlags flags2 = 0;
                Vec3 lookDirection = _player.LookDirection;
                float health = _player.Health;
                Agent.ActionCodeType actionTypeCh0 = Agent.ActionCodeType.Other;
                Agent.ActionCodeType actionTypeCh1 = Agent.ActionCodeType.Other;
                EquipmentIndex wieldedWeaponIndex = _player.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                EquipmentIndex wieldedOffHandWeapon = _player.GetWieldedItemIndex(Agent.HandIndex.OffHand);
                EquipmentHitPoint[] HitPoints = new EquipmentHitPoint[4];
                for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.Weapon4; equipmentIndex++)
                {
                    HitPoints[(int)equipmentIndex] = new EquipmentHitPoint(_otherAgent.Equipment[equipmentIndex].IsShield(), _otherAgent.Equipment[equipmentIndex].HitPoints, equipmentIndex);
                }

                //int damage = MissionOnAgentHitPatch.DamageDone;
                mCache1 = ActionIndexCache.act_none;


                if (_player.Health > 0f)
                {
                    cache1 = _player.GetCurrentAction(0);
                    progress1 = _player.GetCurrentActionProgress(0);
                    flags1 = _player.GetCurrentAnimationFlag(0);
                    cache2 = _player.GetCurrentAction(1);
                    progress2 = _player.GetCurrentActionProgress(1);
                    flags2 = _player.GetCurrentAnimationFlag(1);
                    actionTypeCh0 = _player.GetCurrentActionType(0);
                    actionTypeCh1 = _player.GetCurrentActionType(1);

                    if (_player.HasMount)
                    {
                        mInputVector = _player.MountAgent.GetMovementDirection();
                        mFlags1 = _player.MountAgent.GetCurrentAnimationFlag(1);
                        mProgress1 = _player.MountAgent.GetCurrentActionProgress(1);
                        mCache1 = _player.MountAgent.GetCurrentAction(1);
                    }

                }
                else
                {
                    mCache1 = ActionIndexCache.act_none;
                }


                //InformationManager.DisplayMessage(new InformationMessage("Horse flag: " + mFlags1 + "  Horse progress: " + mProgress1 + "  Horse cache: " + mCache1.Index));




                //InformationManager.DisplayMessage(new InformationMessage(damageDone.ToString()));


                //Vec2 targetPosition = _player.GetTargetPosition();
                //Vec3 targetDirection = _player.GetTargetDirection();
                //InformationManager.DisplayMessage(new InformationMessage("Sending: X: " + lookDirection.x + " Y: " + lookDirection.y + " | Z: " + lookDirection.z));






                //InformationManager.ClearAllMessages();
                //InformationManager.DisplayMessage(new InformationMessage("Sending: " + _player.EventControlFlags.ToString()));
                //InformationManager.DisplayMessage(new InformationMessage(cache2.Name));

                //InformationManager.DisplayMessage(new InformationMessage(_player.GetActionChannelCurrentActionWeight(1).ToString()));

                //throw new Exception();


                if (myPos.IsValid)
                {
                    using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                    {
                        writer.Write(myPos.x);
                        writer.Write(myPos.y);
                        writer.Write(myPos.z);
                        writer.Write(movementFlag);
                        writer.Write(eventFlag);
                        writer.Write(movementDirection.x);
                        writer.Write(movementDirection.y);
                        writer.Write(inputVector.x);
                        writer.Write(inputVector.y);
                        writer.Write(cache1.Index);
                        writer.Write(progress1);
                        writer.Write((ulong)flags1);
                        writer.Write(cache2.Index);
                        writer.Write(progress2);
                        writer.Write((ulong)flags2);
                        writer.Write(lookDirection.x);
                        writer.Write(lookDirection.y);
                        writer.Write(lookDirection.z);
                        writer.Write(health);
                        writer.Write(packetId++);
                        writer.Write((int)actionTypeCh0);
                        writer.Write((int)actionTypeCh1);
                        writer.Write(_player.CrouchMode);

                        writer.Write(mInputVector.x);
                        writer.Write(mInputVector.y);
                        writer.Write((ulong)mFlags1);
                        writer.Write(mProgress1);
                        writer.Write(mCache1.Index);
                        writer.Write(_otherAgent.Health);

                        writer.Write((int)wieldedWeaponIndex);
                        writer.Write((int)wieldedOffHandWeapon);
                        for (int i = 0; i < 4; i++)
                        {
                            writer.Write((int)HitPoints[i].Index);
                            writer.Write(HitPoints[i].HitPoint);
                        }
                        writer.Write(MyAgentShot);
                        if (MyAgentShot)
                        {
                            IOCreateMissile.Write(writer,OnAgentShootMissilePatch.Message);
                            MyAgentShot = false;
                        }
                        // writer.Write(damage);


                            //writer.Write(targetPosition.x);
                            //writer.Write(targetPosition.y);
                            //writer.Write(targetDirection.x);
                            //writer.Write(targetDirection.y);
                            //writer.Write(targetDirection.z);
                    }
                    byte[] bytes = stream.ToArray();
                    if (sender != null && sender.Connected)
                    {
                        sender.Send(bytes);
                    }

                }
            }

        }
    }
}