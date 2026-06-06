//using Common;
//using GameInterface.Registry.Auto;
//using GameInterface.Services.ObjectManager;
//using HarmonyLib;
//using Helpers;
//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using TaleWorlds.CampaignSystem;

//namespace GameInterface.Services.StanceLinks;

///// <summary>
///// Registry for <see cref="StanceLink"/> type
///// </summary>
//internal class StanceLinkRegistry : IAutoRegistry<StanceLink>
//{
//    ILogger Logger { get; }
//    public StanceLinkRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
//    {
//        Logger = logger;

//        autoRegistryFactory.RegisterType(this);
//    }

//    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(StanceLink));

//    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

//    public void RegisterAllObjects(IObjectManager objectManager)
//    {
//        IEnumerable<IFaction> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();
//        IEnumerable<IFaction> clans = Campaign.Current?.Clans ?? Enumerable.Empty<Clan>();

//        var factions = kingdoms.Concat(clans);

//        HashSet<StanceLink> visitedStances = new();

//        foreach (var faction in factions)
//        {

//            int counter = 1;

//            foreach (var stance in FactionHelper.GetStances(faction))
//            {
//                if (visitedStances.Contains(stance)) continue;

//                var networkId = $"{nameof(StanceLink)}_{faction.StringId}_{counter++}";
//                objectManager.AddExisting(networkId, stance);

//                visitedStances.Add(stance);
//            }
//        }
//    }

//    public void OnClientCreated(StanceLink obj, string id)
//    {
//    }

//    public void OnClientDestroyed(StanceLink obj, string id)
//    {
//    }

//    public void OnServerCreated(StanceLink obj, string id)
//    {
//    }

//    public void OnServerDestroyed(StanceLink obj, string id)
//    {
//    }
//}
