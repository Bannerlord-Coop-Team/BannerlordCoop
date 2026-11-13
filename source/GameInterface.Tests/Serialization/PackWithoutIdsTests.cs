using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using Autofac;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>
/// A joining client packs its new hero after removing the hero graph from the object manager, so the
/// host assigns fresh ids. A missing id for those objects is expected and must not log an error.
/// </summary>
public class PackWithoutIdsTests
{
    public sealed class Probe { }

    private sealed class ProbePackage : BinaryPackageBase<Probe>
    {
        public ProbePackage(Probe obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory) { }

        public ProbePackage(Probe obj, IBinaryPackageFactory binaryPackageFactory, ILogger logger) : base(obj, binaryPackageFactory)
        {
            Logger = logger;
        }

        public string Resolve(object obj) => ResolveId(obj);

        protected override void PackInternal() { }

        protected override void UnpackInternal() { }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new List<LogEvent>();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly IBinaryPackageFactory factory;
    private readonly CollectingSink sink = new CollectingSink();
    private readonly ILogger logger;

    public PackWithoutIdsTests()
    {
        GameBootStrap.Initialize();
        factory = SerializationTestContainer.BeginScope().Resolve<IBinaryPackageFactory>();
        logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
    }

    [Fact]
    public void ResolveId_LogsAnErrorForAnObjectWithNoId()
    {
        var package = new ProbePackage(new Probe(), factory, logger);

        package.Resolve(new Probe());

        Assert.Equal(LogEventLevel.Error, Assert.Single(sink.Events).Level);
    }

    [Fact]
    public void ResolveId_LogsNoErrorForAnObjectPackedWithoutAnId()
    {
        var package = new ProbePackage(new Probe(), factory, logger);
        var detached = new Probe();

        string id;
        using (factory.PackWithoutIds(detached))
        {
            id = package.Resolve(detached);
        }

        Assert.Null(id);
        Assert.DoesNotContain(sink.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void ResolveId_LogsAnErrorAgainOnceThePackWithoutIdsEnds()
    {
        var package = new ProbePackage(new Probe(), factory, logger);
        var detached = new Probe();

        using (factory.PackWithoutIds(detached)) { }
        package.Resolve(detached);

        Assert.Equal(LogEventLevel.Error, Assert.Single(sink.Events).Level);
    }

    [Fact]
    public void ResolveId_StillLogsAnErrorForAnotherObjectDuringAPackWithoutIds()
    {
        var package = new ProbePackage(new Probe(), factory, logger);

        using (factory.PackWithoutIds(new Probe()))
        {
            package.Resolve(new Probe());
        }

        Assert.Equal(LogEventLevel.Error, Assert.Single(sink.Events).Level);
    }
}
