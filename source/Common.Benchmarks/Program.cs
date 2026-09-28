using Common.Serialization;
using Common.Tests.Serialization;
using ProtoBuf;
using ProtoBuf.Meta;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Common.Benchmarks;

/// <summary>
/// Legacy versus current <see cref="ProtoBufSerializer.Serialize"/>, per payload and on a movement-shaped workload.
/// Run: dotnet run -c Release -f net6.0 --project source/Common.Benchmarks (or -f net472), add --quick for a smoke run.
/// </summary>
internal static class Program
{
    private static double scale = 1.0;
    private static long sink;

    private static int Main(string[] args)
    {
#if NETFRAMEWORK
        AppDomain.MonitoringIsEnabled = true;
#endif
        if (args.Contains("--quick")) scale = 0.05;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var mapper = new SerializableTypeMapper();
        var legacy = new LegacyProtoBufSerializer(mapper);
        var current = new ProtoBufSerializer(mapper);
        var serializers = new (string Name, ICommonSerializer Serializer)[] { ("legacy", legacy), ("current", current) };

        Assembly protobuf = typeof(Serializer).Assembly;
        Console.WriteLine($"# {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} logical cpus, server gc {System.Runtime.GCSettings.IsServerGC}");
        Console.WriteLine($"# protobuf-net {protobuf.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}, AutoCompile {RuntimeTypeModel.Default.AutoCompile}, scale {scale}");

        var payloads = BenchmarkPayloads.Create(legacy);
        Console.WriteLine();
        Console.WriteLine("## per payload: median ns/op of 7 rounds, allocated B/op, gen0 and gen1 per 100k ops, output length, single-thread ops/s");
        Console.WriteLine("payload | serializer | ns/op | B/op | gen0/100k | gen1/100k | length | ops/s | encode ns | wrap+copy ns");
        foreach ((string name, object payload) in payloads)
        {
            if (!legacy.Serialize(payload).AsSpan().SequenceEqual(current.Serialize(payload)))
                throw new InvalidOperationException($"{name}: current bytes differ from legacy");

            // Encode-only floors: the object-dispatch call legacy makes and the non-generic call.
            Result genericEncode = Measure(() => EncodeOnly(payload, generic: true));
            Result nonGenericEncode = Measure(() => EncodeOnly(payload, generic: false));
            foreach ((string serializerName, ICommonSerializer serializer) in serializers)
            {
                Result r = Measure(() => serializer.Serialize(payload).Length);
                double encode = serializerName == "legacy" ? genericEncode.Ns : nonGenericEncode.Ns;
                Console.WriteLine($"{name} | {serializerName} | {r.Ns:F0} | {r.BytesPerOp:F0} | {r.Gen0Per100k:F1} | {r.Gen1Per100k:F1} | {r.Length} | {1e9 / r.Ns:F0} | {encode:F0} | {r.Ns - encode:F0}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("## movement frame: candidate search, LZ4 attempt and envelope per packet; median ns/frame of 7 rounds");
        Console.WriteLine("workload | serializer | ns/frame | serialize ns | lz4 ns | other ns | B/frame | gen0/1k | gen1/1k | candidates | envelopes | packets | wire bytes | wire hash");
        foreach ((string name, int agents, bool compactIds, bool compressible) in new[]
        {
            ("125 incompressible compact", 125, true, false),
            ("125 compressible compact", 125, true, true),
            ("400 incompressible guid", 400, false, false),
        })
        {
            string? baseline = null;
            foreach ((string serializerName, ICommonSerializer serializer) in serializers)
            {
                var workload = new MovementWorkload(
                    serializer,
                    BenchmarkPayloads.Agents(agents, compressible, seed: 7),
                    compactIds,
                    MovementWorkload.RelayPayloadBudget(serializer));
                Result r = Measure(() => { workload.SendFrame(); return 0; }, perOps: 1000);
                MovementFrameStats timed = workload.Stats;
                double ticksToNs = 1e9 / Stopwatch.Frequency / timed.Frames;
                double serializeNs = timed.SerializeTicks * ticksToNs;
                double lz4Ns = timed.Lz4Ticks * ticksToNs;
                workload.ResetStats();
                workload.SendFrame();
                MovementFrameStats s = workload.Stats;
                string counts = $"{s.Candidates} | {s.Envelopes} | {s.Packets} | {s.WireBytes} | {s.WireHash:X16}";
                Console.WriteLine($"{name} | {serializerName} | {r.Ns:F0} | {serializeNs:F0} | {lz4Ns:F0} | {r.Ns - serializeNs - lz4Ns:F0} | {r.BytesPerOp:F0} | {r.Gen0Per100k:F1} | {r.Gen1Per100k:F1} | {counts}");
                if (baseline == null) baseline = counts;
                else if (baseline != counts) throw new InvalidOperationException($"{name}: movement counts differ from legacy");
            }
        }

        Console.WriteLine();
        Console.WriteLine("## parallel throughput: 8 threads for 2 s on movement 60 compact, total ops");
        object movement = payloads.Single(p => p.Name == "movement 60 compact").Value;
        foreach ((string serializerName, ICommonSerializer serializer) in serializers)
            Console.WriteLine($"{serializerName} | {ParallelOps(serializer, movement, threads: 8, TimeSpan.FromSeconds(2 * scale))}");

        Console.WriteLine($"(sink {sink})");
        return 0;
    }

    private static readonly MemoryStream EncodeStream = new MemoryStream(1 << 20);

    private static int EncodeOnly(object payload, bool generic)
    {
        EncodeStream.SetLength(0);
        if (generic) Serializer.Serialize(EncodeStream, payload);
        else RuntimeTypeModel.Default.Serialize(EncodeStream, payload);
        return (int)EncodeStream.Length;
    }

    private static long ParallelOps(ICommonSerializer serializer, object payload, int threads, TimeSpan duration)
    {
        long ops = 0;
        using var barrier = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
        {
            barrier.SignalAndWait();
            var sw = Stopwatch.StartNew();
            long local = 0;
            while (sw.Elapsed < duration)
            {
                serializer.Serialize(payload);
                local++;
            }
            Interlocked.Add(ref ops, local);
        })).ToList();
        workers.ForEach(t => t.Start());
        workers.ForEach(t => t.Join());
        return ops;
    }

    private readonly struct Result
    {
        public double Ns { get; }
        public double BytesPerOp { get; }
        public double Gen0Per100k { get; }
        public double Gen1Per100k { get; }
        public int Length { get; }

        public Result(double ns, double bytesPerOp, double gen0Per100k, double gen1Per100k, int length)
        {
            Ns = ns;
            BytesPerOp = bytesPerOp;
            Gen0Per100k = gen0Per100k;
            Gen1Per100k = gen1Per100k;
            Length = length;
        }
    }

    private static long AllocatedBytes()
    {
#if NETFRAMEWORK
        return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
#else
        return GC.GetAllocatedBytesForCurrentThread();
#endif
    }

    // Warm up, size the rounds to about 120 ms, then report the median round and the totals.
    private static Result Measure(Func<int> operation, int perOps = 100_000)
    {
        var sw = Stopwatch.StartNew();
        long warmup = 0;
        int length = 0;
        while (sw.Elapsed.TotalMilliseconds < 300 * scale)
        {
            length = operation();
            warmup++;
        }

        long iterations = Math.Max(20, (long)(warmup * 120.0 / 300.0));
        const int rounds = 7;
        var times = new double[rounds];
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long bytes0 = AllocatedBytes();
        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        for (int round = 0; round < rounds; round++)
        {
            sw.Restart();
            for (long i = 0; i < iterations; i++) sink += operation();
            sw.Stop();
            times[round] = sw.Elapsed.TotalMilliseconds * 1e6 / iterations;
        }
        long bytes1 = AllocatedBytes();
        double ops = iterations * (double)rounds;
        Array.Sort(times);
        return new Result(
            times[rounds / 2],
            (bytes1 - bytes0) / ops,
            (GC.CollectionCount(0) - gen0) * perOps / ops,
            (GC.CollectionCount(1) - gen1) * perOps / ops,
            length);
    }
}
