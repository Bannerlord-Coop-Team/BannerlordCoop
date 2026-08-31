using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using Common.Commands;
using Common.Logging;
using Common.PacketHandlers;
using GameInterface.AutoSync;
using GameInterface.Configuration;
using GameInterface.Registry;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Armies;
using GameInterface.Services.Bandits;
using GameInterface.Services.Barters;
using GameInterface.Services.BugReporting;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Modules;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.Stances;
using GameInterface.Services.Time;
using GameInterface.Services.TroopRosters;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.Patches;
using GameInterface.Services.Workshops;
using GameInterface.Surrogates;
using GameInterface.Utils.Commands;
using HarmonyLib;
using Serilog;
using System.Linq;
using System.Threading;

namespace GameInterface;

public class GameInterfaceModule : Module
{
    // TODO move to config
    public const string HarmonyId = "Bannerlord.Coop";

    private static readonly Harmony harmony = new Harmony(HarmonyId);

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(harmony).As<Harmony>().SingleInstance();
        builder.RegisterInstance(new CoopLogFile(null))
            .As<ICoopLogFile>()
            .SingleInstance()
            .PreserveExistingDefaults();
        builder.Register(_ => new CancellationTokenSource())
            .InstancePerLifetimeScope()
            .PreserveExistingDefaults();

        builder.RegisterType<SurrogateCollection>().As<ISurrogateCollection>().InstancePerLifetimeScope().AutoActivate();

        builder.RegisterType<CoopCommandArgsFactory>().As<ICoopCommandArgsFactory>().InstancePerDependency();
        builder.RegisterType<CoopCommandRegistry>().As<ICoopCommandRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<LegacyCoopCommandExecutor>()
            .As<ILegacyCoopCommandExecutor>()
            .InstancePerDependency();
        builder.RegisterAssemblyTypes(typeof(GameInterfaceModule).Assembly)
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .As(type => type.GetInterfaces()
                .Where(interfaceType => interfaceType != typeof(ICoopCommand)))
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Arenas.Commands.ViewArenaMasterInteractionsCommandCoopCommand>()
            .As<Services.Arenas.Commands.IViewArenaMasterInteractionsCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.SetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand>()
            .As<Services.BesiegerCamps.Commands.ISetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.SetBesiegerCampPreparationsProgressCoopCommand>()
            .As<Services.BesiegerCamps.Commands.ISetBesiegerCampPreparationsProgressCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.SetBesiegerCampSiegeStrategyCoopCommand>()
            .As<Services.BesiegerCamps.Commands.ISetBesiegerCampSiegeStrategyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.SetBesiegerCampLeaderPartyCoopCommand>()
            .As<Services.BesiegerCamps.Commands.ISetBesiegerCampLeaderPartyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.AddPartyToBesiegerCampCoopCommand>()
            .As<Services.BesiegerCamps.Commands.IAddPartyToBesiegerCampCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.BesiegerCamps.Commands.RemovePartyFromBesiegerCampCoopCommand>()
            .As<Services.BesiegerCamps.Commands.IRemovePartyFromBesiegerCampCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.EnterLocationCoopCommand>()
            .As<Services.Locations.Commands.IEnterLocationCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.LeaveLocationCoopCommand>()
            .As<Services.Locations.Commands.ILeaveLocationCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.ListLocationsCoopCommand>()
            .As<Services.Locations.Commands.IListLocationsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.InfoCoopCommand>()
            .As<Services.Locations.Commands.IInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.ListCharactersCoopCommand>()
            .As<Services.Locations.Commands.IListCharactersCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.ListSpecialItemsCoopCommand>()
            .As<Services.Locations.Commands.IListSpecialItemsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.AddCharacterCoopCommand>()
            .As<Services.Locations.Commands.IAddCharacterCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.RemoveCharacterCoopCommand>()
            .As<Services.Locations.Commands.IRemoveCharacterCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.RemoveAllCharactersCoopCommand>()
            .As<Services.Locations.Commands.IRemoveAllCharactersCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.AddSpecialItemCoopCommand>()
            .As<Services.Locations.Commands.IAddSpecialItemCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.RemoveSpecialItemCoopCommand>()
            .As<Services.Locations.Commands.IRemoveSpecialItemCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Locations.Commands.PopulateCoopCommand>()
            .As<Services.Locations.Commands.IPopulateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.SetupCoopCommand>()
            .As<Services.MobileParties.Commands.ISetupCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.FollowCoopCommand>()
            .As<Services.MobileParties.Commands.IFollowCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.MoveTargetCoopCommand>()
            .As<Services.MobileParties.Commands.IMoveTargetCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.StateCoopCommand>()
            .As<Services.MobileParties.Commands.IStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.RestoreCoopCommand>()
            .As<Services.MobileParties.Commands.IRestoreCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.RefreshMercenaryStocksCoopCommand>()
            .As<Services.MobileParties.Commands.IRefreshMercenaryStocksCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.RequestMercenaryStockCoopCommand>()
            .As<Services.MobileParties.Commands.IRequestMercenaryStockCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.InfoCoopCommand>()
            .As<Services.MobileParties.Commands.IInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.ComponentInfoCoopCommand>()
            .As<Services.MobileParties.Commands.IComponentInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.AttachmentIdsCoopCommand>()
            .As<Services.MobileParties.Commands.IAttachmentIdsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.VerifyAiAuthorityCoopCommand>()
            .As<Services.MobileParties.Commands.IVerifyAiAuthorityCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.CreateNewPartyCoopCommand>()
            .As<Services.MobileParties.Commands.ICreateNewPartyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.SpawnTestPartiesCoopCommand>()
            .As<Services.MobileParties.Commands.ISpawnTestPartiesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.DestroyPartyCoopCommand>()
            .As<Services.MobileParties.Commands.IDestroyPartyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.DestroyAllBanditPartiesCoopCommand>()
            .As<Services.MobileParties.Commands.IDestroyAllBanditPartiesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.ListMobilePartiesCoopCommand>()
            .As<Services.MobileParties.Commands.IListMobilePartiesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.SetWagePaymentLimitCoopCommand>()
            .As<Services.MobileParties.Commands.ISetWagePaymentLimitCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.SetUnlimitedWageToggleCoopCommand>()
            .As<Services.MobileParties.Commands.ISetUnlimitedWageToggleCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.MobileParties.Commands.AuditPartiesCoopCommand>()
            .As<Services.MobileParties.Commands.IAuditPartiesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.AuditCoopCommand>()
            .As<Services.Settlements.Commands.IAuditCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.EnterRandomCastleCoopCommand>()
            .As<Services.Settlements.Commands.IEnterRandomCastleCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#if DEBUG
        builder.RegisterType<Services.Settlements.Commands.TeleportMainPartyToCastleCoopCommand>()
            .As<Services.Settlements.Commands.ITeleportMainPartyToCastleCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.RestoreMainPartyCastleTeleportCoopCommand>()
            .As<Services.Settlements.Commands.IRestoreMainPartyCastleTeleportCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
        builder.RegisterType<Services.Settlements.Commands.GetTownNameCoopCommand>()
            .As<Services.Settlements.Commands.IGetTownNameCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetEnemiesSpottedCoopCommand>()
            .As<Services.Settlements.Commands.ISetEnemiesSpottedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetAlliesSpottedCoopCommand>()
            .As<Services.Settlements.Commands.ISetAlliesSpottedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetBribePaidCoopCommand>()
            .As<Services.Settlements.Commands.ISetBribePaidCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetHitPointsCoopCommand>()
            .As<Services.Settlements.Commands.ISetHitPointsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetLastAttackerPartyCoopCommand>()
            .As<Services.Settlements.Commands.ISetLastAttackerPartyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.ListSiegeStatesCoopCommand>()
            .As<Services.Settlements.Commands.IListSiegeStatesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetSiegeStateCoopCommand>()
            .As<Services.Settlements.Commands.ISetSiegeStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetMiltiiaCoopCommand>()
            .As<Services.Settlements.Commands.ISetMiltiiaCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetGarrisonWageLimitCoopCommand>()
            .As<Services.Settlements.Commands.ISetGarrisonWageLimitCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.CollectCacheNotablesCoopCommand>()
            .As<Services.Settlements.Commands.ICollectCacheNotablesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.InfoCoopCommand>()
            .As<Services.Settlements.Commands.IInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetOwnerCoopCommand>()
            .As<Services.Settlements.Commands.ISetOwnerCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.CaptureBySiegeCoopCommand>()
            .As<Services.Settlements.Commands.ICaptureBySiegeCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.OwnerStateCoopCommand>()
            .As<Services.Settlements.Commands.IOwnerStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetGoldCoopCommand>()
            .As<Services.Settlements.Commands.ISetGoldCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetIsOwnerUnassignedCoopCommand>()
            .As<Services.Settlements.Commands.ISetIsOwnerUnassignedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Settlements.Commands.SetOwnerClanCoopCommand>()
            .As<Services.Settlements.Commands.ISetOwnerClanCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StartPrisonerPromptFixtureCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStartPrisonerPromptFixtureCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.PrisonerPromptFixtureStateCoopCommand>()
            .As<Services.SiegeEvents.Commands.IPrisonerPromptFixtureStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.RestorePrisonerPromptFixtureCoopCommand>()
            .As<Services.SiegeEvents.Commands.IRestorePrisonerPromptFixtureCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StartArmyReliefCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStartArmyReliefCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.ArmyReliefStateCoopCommand>()
            .As<Services.SiegeEvents.Commands.IArmyReliefStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.RequestBesiegeCoopCommand>()
            .As<Services.SiegeEvents.Commands.IRequestBesiegeCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.RequestAssaultCoopCommand>()
            .As<Services.SiegeEvents.Commands.IRequestAssaultCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.JoinActiveAssaultCoopCommand>()
            .As<Services.SiegeEvents.Commands.IJoinActiveAssaultCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.AssaultEntryStateCoopCommand>()
            .As<Services.SiegeEvents.Commands.IAssaultEntryStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.LeaveCoopCommand>()
            .As<Services.SiegeEvents.Commands.ILeaveCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.LeaveSettlementCoopCommand>()
            .As<Services.SiegeEvents.Commands.ILeaveSettlementCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StartSiegeCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStartSiegeCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StopSiegeCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStopSiegeCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.JoinPlayersCoopCommand>()
            .As<Services.SiegeEvents.Commands.IJoinPlayersCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.PlayerStateCoopCommand>()
            .As<Services.SiegeEvents.Commands.IPlayerStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.PrepareLaddersOnlyCoopCommand>()
            .As<Services.SiegeEvents.Commands.IPrepareLaddersOnlyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StageMachinesCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStageMachinesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.StartAssaultCoopCommand>()
            .As<Services.SiegeEvents.Commands.IStartAssaultCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.TerminalStatusCoopCommand>()
            .As<Services.SiegeEvents.Commands.ITerminalStatusCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.ResolveStarvationCoopCommand>()
            .As<Services.SiegeEvents.Commands.IResolveStarvationCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.ListSiegesCoopCommand>()
            .As<Services.SiegeEvents.Commands.IListSiegesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.GraphStateCoopCommand>()
            .As<Services.SiegeEvents.Commands.IGraphStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.FocusSettlementCoopCommand>()
            .As<Services.SiegeEvents.Commands.IFocusSettlementCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.DumpPartyCoopCommand>()
            .As<Services.SiegeEvents.Commands.IDumpPartyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.DumpEnginesCoopCommand>()
            .As<Services.SiegeEvents.Commands.IDumpEnginesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.SiegeEvents.Commands.DumpMachinesCoopCommand>()
            .As<Services.SiegeEvents.Commands.IDumpMachinesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Tournaments.Commands.AddTournamentToTownCoopCommand>()
            .As<Services.Tournaments.Commands.IAddTournamentToTownCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.BeginDanusticaFixtureCoopCommand>()
            .As<Services.Tournaments.Commands.IBeginDanusticaFixtureCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.DanusticaFixtureStateCoopCommand>()
            .As<Services.Tournaments.Commands.IDanusticaFixtureStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.RestoreDanusticaFixtureCoopCommand>()
            .As<Services.Tournaments.Commands.IRestoreDanusticaFixtureCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.AbortDanusticaFixtureCoopCommand>()
            .As<Services.Tournaments.Commands.IAbortDanusticaFixtureCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.RequestDanusticaJoinCoopCommand>()
            .As<Services.Tournaments.Commands.IRequestDanusticaJoinCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.RequestDanusticaStartCoopCommand>()
            .As<Services.Tournaments.Commands.IRequestDanusticaStartCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.RequestDanusticaChoiceCoopCommand>()
            .As<Services.Tournaments.Commands.IRequestDanusticaChoiceCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.RequestDanusticaLeaveCoopCommand>()
            .As<Services.Tournaments.Commands.IRequestDanusticaLeaveCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
#if DEBUG
        builder.RegisterType<Services.Tournaments.Commands.ObserveDanusticaCommandCoopCommand>()
            .As<Services.Tournaments.Commands.IObserveDanusticaCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
        builder.RegisterType<Services.Towns.Commands.AuditorCoopCommand>()
            .As<Services.Towns.Commands.IAuditorCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ListTownsCoopCommand>()
            .As<Services.Towns.Commands.IListTownsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ListItemsCoopCommand>()
            .As<Services.Towns.Commands.IListItemsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.InfoCoopCommand>()
            .As<Services.Towns.Commands.IInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.GarrisonBacklinkCoopCommand>()
            .As<Services.Towns.Commands.IGarrisonBacklinkCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.FocusGarrisonCoopCommand>()
            .As<Services.Towns.Commands.IFocusGarrisonCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ApplyGarrisonLifecycleCoopCommand>()
            .As<Services.Towns.Commands.IApplyGarrisonLifecycleCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ListBuildingsCoopCommand>()
            .As<Services.Towns.Commands.IListBuildingsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ListWorkshopsCoopCommand>()
            .As<Services.Towns.Commands.IListWorkshopsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetFoodStocksCoopCommand>()
            .As<Services.Towns.Commands.ISetFoodStocksCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownGovernorCoopCommand>()
            .As<Services.Towns.Commands.ISetTownGovernorCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownLastCapturedByCoopCommand>()
            .As<Services.Towns.Commands.ISetTownLastCapturedByCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.AddToTownSoldItemsCoopCommand>()
            .As<Services.Towns.Commands.IAddToTownSoldItemsCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownProsperityCoopCommand>()
            .As<Services.Towns.Commands.ISetTownProsperityCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownLoyaltyCoopCommand>()
            .As<Services.Towns.Commands.ISetTownLoyaltyCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownSecurityCoopCommand>()
            .As<Services.Towns.Commands.ISetTownSecurityCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownInRebelliousStateCoopCommand>()
            .As<Services.Towns.Commands.ISetTownInRebelliousStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.StartRebellionCoopCommand>()
            .As<Services.Towns.Commands.IStartRebellionCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTownGarrisonAutoRecruitmentIsEnabledCoopCommand>()
            .As<Services.Towns.Commands.ISetTownGarrisonAutoRecruitmentIsEnabledCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.SetTradeTaxAccumulatedCoopCommand>()
            .As<Services.Towns.Commands.ISetTradeTaxAccumulatedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ChangeCurrentBuildingCoopCommand>()
            .As<Services.Towns.Commands.IChangeCurrentBuildingCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ChangeCurrentBuildingQueueCoopCommand>()
            .As<Services.Towns.Commands.IChangeCurrentBuildingQueueCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Towns.Commands.ViewManagementDataCoopCommand>()
            .As<Services.Towns.Commands.IViewManagementDataCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.AllowRaidAiInterventionCoopCommand>()
            .As<Services.Villages.Commands.IAllowRaidAiInterventionCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.ListVillagesCoopCommand>()
            .As<Services.Villages.Commands.IListVillagesCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.InfoCoopCommand>()
            .As<Services.Villages.Commands.IInfoCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.SetVillageStateCoopCommand>()
            .As<Services.Villages.Commands.ISetVillageStateCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.SetVillageHearthCoopCommand>()
            .As<Services.Villages.Commands.ISetVillageHearthCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.SetTradeTaxAccumulatedCoopCommand>()
            .As<Services.Villages.Commands.ISetTradeTaxAccumulatedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.SetLastDemandTimeSatisifiedCoopCommand>()
            .As<Services.Villages.Commands.ISetLastDemandTimeSatisifiedCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.ViewInteractedVillagersCommandCoopCommand>()
            .As<Services.Villages.Commands.IViewInteractedVillagersCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Villages.Commands.ViewLootedVillagersCoopCommand>()
            .As<Services.Villages.Commands.IViewLootedVillagersCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.SetWorkshopCustomNameCoopCommand>()
            .As<Services.Workshops.Commands.ISetWorkshopCustomNameCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.SetWorkshopOwnerCoopCommand>()
            .As<Services.Workshops.Commands.ISetWorkshopOwnerCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.OwnersInSettlementCommandCoopCommand>()
            .As<Services.Workshops.Commands.IOwnersInSettlementCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.HeroOwnedWorkshopsCommandCoopCommand>()
            .As<Services.Workshops.Commands.IHeroOwnedWorkshopsCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.ViewWarehouseRostersCommandCoopCommand>()
            .As<Services.Workshops.Commands.IViewWarehouseRostersCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<Services.Workshops.Commands.ViewWorkshopInfoCommandCoopCommand>()
            .As<Services.Workshops.Commands.IViewWorkshopInfoCommandCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<CoopCommandLineRegistrar>()
            .As<ICoopCommandLineRegistrar>()
            .InstancePerLifetimeScope()
            .AutoActivate();

        builder.RegisterType<GameInterface>().As<IGameInterface>().InstancePerLifetimeScope().AutoActivate();
        // mod-config.json: one lazy read per session container (see IModConfig).
        builder.RegisterType<ModConfig>().As<IModConfig>().InstancePerLifetimeScope();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerManager>().As<IPlayerManager>().InstancePerLifetimeScope();
        builder.RegisterType<BugReportService>().As<IBugReportService>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<BugReportOverlay>().As<IBugReportOverlay>().InstancePerLifetimeScope();
        builder.RegisterType<CoopLogSnapshotProvider>().As<ICoopLogSnapshotProvider>().InstancePerDependency();
        builder.RegisterType<BugReportServerSaveProvider>().As<IBugReportServerSaveProvider>().InstancePerDependency();
        builder.RegisterType<BugReportArchiveBuilder>().As<IBugReportArchiveBuilder>().InstancePerDependency();
        builder.RegisterType<BugReportLogValidator>().As<IBugReportLogValidator>().InstancePerDependency();
        builder.RegisterType<BugReportUploader>().As<IBugReportUploader>().InstancePerDependency();
        builder.RegisterType<BugReportLogSharingPreference>().As<IBugReportLogSharingPreference>().InstancePerDependency();
        builder.RegisterType<BugReportSubmissionConsent>().As<IBugReportSubmissionConsent>().InstancePerDependency();
        builder.RegisterType<KillFeedOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<MapTimeOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<BugReportOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<ChatOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<PlayerNameplatesOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<NetworkOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<LocalMovementBandwidth>().As<ILocalMovementBandwidth>().InstancePerDependency();
        builder.RegisterType<ChatPlayerName>().As<IChatPlayerNameResolver>().InstancePerDependency();
        builder.RegisterType<PlayerPartyRestorer>().As<IPlayerPartyRestorer>().InstancePerDependency();
        builder.RegisterType<PlayerCreationRollback>().As<IPlayerCreationRollback>().InstancePerDependency();
        builder.RegisterType<MobilePartyBehaviorSnapshot>().As<IMobilePartyBehaviorSnapshot>().InstancePerDependency();
        builder.RegisterType<BarterClientPresentation>().As<IBarterClientPresentation>().InstancePerDependency();
        builder.RegisterType<SafePassagePartyResolver>().AsSelf().As<ISafePassagePartyResolver>().InstancePerDependency();
        builder.RegisterType<PeacePursuitCleaner>().As<IPeacePursuitCleaner>().InstancePerDependency();
        builder.RegisterType<PartyVisibilitySweep>().As<IPartyVisibilitySweep>().InstancePerDependency();
        builder.RegisterType<ConversationRestartContextTracker>().As<IConversationRestartContextTracker>().InstancePerLifetimeScope();
        builder.RegisterType<IssueConversationTracker>().As<IIssueConversationTracker>().InstancePerLifetimeScope();
        builder.RegisterType<IssueOwnershipRegistry>().As<IIssueOwnershipRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<IssueGenerationRegistry>().As<IIssueGenerationRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<AwaitingAlternativeSolutionTroopsRegistry>().As<IAwaitingAlternativeSolutionTroopsRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<BattleHostRegistry>().As<IBattleHostRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<LocationHostRegistry>().As<ILocationHostRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<LocationConversationAgentGuard>().As<ILocationConversationAgentGuard>().InstancePerDependency();
        builder.RegisterType<BattleAgentBudget>().As<IBattleAgentBudget>().InstancePerDependency();
        builder.RegisterType<NearbyPartyReinforcer>().As<INearbyPartyReinforcer>().InstancePerDependency();
        builder.RegisterType<SiegeMapEventLeaderReconciler>().As<ISiegeMapEventLeaderReconciler>().InstancePerDependency();
        builder.RegisterType<AiSiegeAssaultReadiness>().As<IAiSiegeAssaultReadiness>().InstancePerDependency();
        builder.RegisterType<AiSiegeTerminalPolicy>().As<IAiSiegeTerminalPolicy>().InstancePerLifetimeScope();
        builder.RegisterType<SiegeEventGraphSynchronizer>().As<ISiegeEventGraphSynchronizer>().InstancePerDependency();
        builder.RegisterType<SiegeJoinMenuActivationGate>().As<ISiegeJoinMenuActivationGate>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventContributionBarrier>().As<IMapEventContributionBarrier>().InstancePerDependency();
        builder.RegisterType<ArmyDisbander>().As<IArmyDisbander>().InstancePerDependency();
        builder.RegisterType<PlayerSiegeTargetScoring>().As<IPlayerSiegeTargetScoring>().InstancePerDependency();
        builder.RegisterType<ArmyFormationPositionConvergence>().As<IArmyFormationPositionConvergence>().InstancePerDependency();
        builder.RegisterType<MapEventLoadCleaner>().As<IMapEventLoadCleaner>().InstancePerDependency();
        builder.RegisterType<EncounterMenuConditionRefresher>().As<IEncounterMenuConditionRefresher>().InstancePerDependency();
        builder.RegisterType<PartyScreenRosterRefresher>().As<IPartyScreenRosterRefresher>().InstancePerDependency();
        builder.RegisterType<PlayerTroopXpRelevance>().As<IPlayerTroopXpRelevance>().InstancePerDependency();
        builder.RegisterType<PrisonerSaleValidator>().As<IPrisonerSaleValidator>().InstancePerDependency();
        builder.RegisterType<PlayerRansomReleaseSettlementProvider>().As<IPlayerRansomReleaseSettlementProvider>().InstancePerDependency();
        builder.RegisterType<SessionHeroMeetingDataInterface>().As<ISessionHeroMeetingDataInterface>().InstancePerDependency();
        builder.RegisterType<PrisonerSaleProcessor>().As<IPrisonerSaleProcessor>().InstancePerDependency();
        builder.RegisterType<PartyScreenRosterBaselineProvider>().As<IPartyScreenRosterBaselineProvider>().InstancePerDependency();
        builder.RegisterType<BanditPartyHomeSettlementRepairer>().As<IBanditPartyHomeSettlementRepairer>().InstancePerDependency();
        builder.RegisterType<DeadHeroCaptivityRepairer>().As<IDeadHeroCaptivityRepairer>().InstancePerDependency();
        builder.RegisterType<WorkshopRepairer>().As<IWorkshopRepairer>().InstancePerDependency();
        builder.RegisterType<ModuleRescanCompletionRunner>().As<IModuleRescanCompletionRunner>().InstancePerDependency();
        builder.RegisterType<MapEventLogger>().As<IMapEventLogger>().InstancePerLifetimeScope();
        builder.RegisterType<TroopRosterLogger>().As<ITroopRosterLogger>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceClock>().As<IPartySyncPerformanceClock>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceFileWriter>().As<IPartySyncPerformanceFileWriter>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformancePartyProvider>().As<IPartySyncPerformancePartyProvider>().InstancePerLifetimeScope();
        builder.RegisterType<LiveTestCommandDispatcher>().As<ILiveTestCommandDispatcher>().InstancePerDependency();
        builder.RegisterType<CoopModulePathResolver>().As<ICoopModulePathResolver>().InstancePerDependency();
        builder.RegisterType<FixedTownNpcService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<LocationNpcGateState>().As<ILocationNpcGate>().InstancePerLifetimeScope();
        builder.RegisterType<LocationConversationClientState>()
            .As<ILocationConversationClientState>()
            .InstancePerLifetimeScope();
        builder.RegisterType<SettlementHeroSpawnPool>()
            .As<ISettlementHeroSpawnPool>()
            .InstancePerDependency();
        builder.RegisterType<KingdomCreationSettlementTracker>().AsSelf().As<IKingdomCreationSettlementTracker>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomCreator>().AsSelf().As<IKingdomCreator>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeResolver>().AsSelf().As<IKingdomDecisionOutcomeResolver>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeOrder>().AsSelf().As<IKingdomDecisionOutcomeOrder>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionRoundPresentation>().AsSelf().As<IKingdomDecisionRoundPresentation>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionVoteManager>().AsSelf().As<IKingdomDecisionVoteManager>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomMembershipState>().AsSelf().As<IKingdomMembershipState>().InstancePerLifetimeScope();
        builder.RegisterType<ClientClanStrengthRefresher>().As<IClientClanStrengthRefresher>().InstancePerDependency();
        builder.RegisterType<MainPartyBattleRewardsCache>().As<IMainPartyBattleRewardsCache>().InstancePerLifetimeScope();
        builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventInitializationBarrierBinding>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<MapTrackerProviderHolder>().As<IMapTrackerProviderHolder>().InstancePerLifetimeScope();

        builder.RegisterModule<ServiceModule>();
        builder.RegisterModule<ObjectManagerModule>();
        builder.RegisterModule<RegistryModule>();
        builder.RegisterModule<AutoSyncModule>();


        base.Load(builder);
    }

    // Log injector
    protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
    {
        registration.PipelineBuilding += (sender, pipeline) =>
        {
            pipeline.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (c, next) =>
            {
                var forType = c.Registration.Activator.LimitType;

                var logParameter = new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(ILogger),
                    (p, c) => AccessTools.Method(typeof(LogManager), nameof(LogManager.GetLogger)).MakeGenericMethod(forType).Invoke(null, null) as ILogger);

                c.GetType().Property(nameof(c.Parameters)).SetValue(c, c.Parameters.Union(new[] { logParameter }));

                next(c);
            });
        };
    }
}
