//using CoopFramework;
//using JetBrains.Annotations;
//using Sync.Behaviour;
//using Sync.Call;
//using Sync.Value;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.CampaignBehaviors;
//using TaleWorlds.CampaignSystem.Settlements;

//namespace Coop.Mod.GameSync
//{
//    class TownSync : CoopManaged<TownSync, Town>
//    {
//        static TownSync()
//        {
//            When(GameLoop)
//                .Calls(new PatchedInvokable[] 
//                { 
//                    Setter(nameof(Town.Loyalty)),
//                    Setter(nameof(Town.Security)),
//                    Setter(nameof(Town.OwnerClan)),
//                    Setter(nameof(Town.TradeTaxAccumulated)),
//                    Setter(nameof(Town.Governor))
//                })
//                .Broadcast(() => CoopClient.Instance.Synchronization)
//                .DelegateTo(IsServer);
//            FieldBehaviourBuilder fieldBehaviourBuilder = 
//                When(GameLoop)
//                .Changes(new FieldAccess[]
//                {
//                    Field<int>("_wallLevel"),
//                    Field<bool>(nameof(Town.GarrisonAutoRecruitmentIsEnabled)),
//                    Field<TownMarketData>("_marketData"),
//                    Field<Town.SellLog>("_soldItems"),
//                    Field<int>(nameof(Town.BoostBuildingProcess)),
//                    Field<bool>(nameof(Town.InRebelliousState)),
//                    Field<bool>("_isCastle")
//                })
//                .Through(new PatchedInvokable[]
//                {
//                    Method(nameof(Town.Deserialize)),
//                    Method(nameof(Town.SetSoldItems))

//                });
//            if (Coop.IsServer)
//                fieldBehaviourBuilder.Keep();
//            else
//                fieldBehaviourBuilder.Revert();
//            When(GameLoop)
//                .Calls(new PatchedInvokable[] 
//                {
//                    Method("DailyTick"),
//                    Method(nameof(Town.Buildings.Add)),
//                    Method(nameof(Town.Buildings.RemoveAt)),
//                    Method(nameof(Town.BuildingsInProgress.Enqueue)),
//                    Method(nameof(Town.BuildingsInProgress.Dequeue))
//                })
//                .DelegateTo(IsServer);

//            ApplyStaticPatches();
//            AutoWrapAllInstances(c => new TownSync(c));
//        }

//        public TownSync([NotNull] Town instance) : base(instance)
//        {
//        }

//        private static ECallPropagation IsServer(IPendingMethodCall call)
//        {
//            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
//        }

//        private class UpdateAllowed : IActionValidator
//        {
//            public bool IsAllowed()
//            {
//                return Coop.IsServer;
//            }

//            public string GetReasonForRejection()
//            {
//                return "Not Authorized";
//            }
//        }


//        #region Utils
//        public static TownSync MakeManaged(Town town)
//        {
//            if (Instances.TryGetValue(town, out TownSync instance))
//            {
//                return instance;
//            }
//            return new TownSync(town);
//        }
//        #endregion
//    }
//}
