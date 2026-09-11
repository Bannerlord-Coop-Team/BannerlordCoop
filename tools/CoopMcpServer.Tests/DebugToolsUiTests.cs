using Common.LiveTesting;
using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class DebugToolsUiTests
{
    private readonly IRunOrchestrator runs = DispatchProxy.Create<IRunOrchestrator, RequestRecorder>();
    private RequestRecorder Recorder => (RequestRecorder)runs;
    private const string Handle = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task DiscoverySelectedInspectionPagingAndActionKeepExplicitProtocolScope()
    {
        var tools = new DebugTools(runs);
        await tools.UiLayers("run", "client1", default);
        Assert.Equal("ui-layers", Recorder.Method);
        Assert.False(Recorder.Mutation);
        await tools.UiInspect("run", "client1", default, layer: Handle);
        Assert.Equal("ui-inspect", Recorder.Method);
        Assert.Equal(Handle, Recorder.Parameters.GetProperty("layer").GetString());
        Assert.Equal(JsonValueKind.Null, Recorder.Parameters.GetProperty("snapshot").ValueKind);
        await tools.UiInspect("run", "client1", default, Handle, 128);
        Assert.Equal(128, Recorder.Parameters.GetProperty("offset").GetInt32());
        await tools.UiAction("run", "client1", Handle, "e1", "click", default);
        Assert.Equal("ui-action", Recorder.Method);
        Assert.True(Recorder.Mutation);
        Assert.Equal(4, Recorder.Count);
    }

    [Fact]
    public async Task AllLayerAndOptionsToolsRemainAvailable()
    {
        var tools = new DebugTools(runs);
        await tools.UiInspect("run", "client1", default);
        Assert.Equal(JsonValueKind.Null, Recorder.Parameters.GetProperty("layer").ValueKind);
        await tools.OptionsMenu("run", "client1", "inspect", default);
        Assert.Equal("options-menu", Recorder.Method);
        Assert.False(Recorder.Mutation);
    }

    [Theory]
    [InlineData(null, -1, null)]
    [InlineData(null, 1, null)]
    [InlineData(Handle, 16385, null)]
    [InlineData("bad", 0, null)]
    [InlineData(null, 0, "bad")]
    public async Task InvalidInspectionArgumentsNeverReachPipe(string snapshot, int offset, string layer)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new DebugTools(runs).UiInspect("run", "client1", default, snapshot, offset, layer));
        Assert.Equal(0, Recorder.Count);
    }

    [Theory]
    [InlineData(null, "e0", "click", null)]
    [InlineData(Handle, "button", "click", null)]
    [InlineData(Handle, "e-1", "click", null)]
    [InlineData(Handle, "e0", "invoke", null)]
    [InlineData(Handle, "e0", "slider", double.NaN)]
    public async Task InvalidActionArgumentsNeverReachPipe(string snapshot, string element, string action, double? value)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new DebugTools(runs).UiAction("run", "client1", snapshot, element, action, default, value: value));
        Assert.Equal(0, Recorder.Count);
    }

    [Fact]
    public void ToolSchemasExposeExplicitOptionalLayerAndOneShotAction()
    {
        var inspect = typeof(DebugTools).GetMethod(nameof(DebugTools.UiInspect));
        Assert.Equal("ui_inspect", inspect.GetCustomAttribute<McpServerToolAttribute>().Name);
        var layer = inspect.GetParameters().Single(p => p.Name == "layer");
        Assert.True(layer.IsOptional);
        Assert.Equal(typeof(string), layer.ParameterType);
        Assert.Equal("ui_layers", typeof(DebugTools).GetMethod(nameof(DebugTools.UiLayers)).GetCustomAttribute<McpServerToolAttribute>().Name);
        Assert.Equal("ui_action", typeof(DebugTools).GetMethod(nameof(DebugTools.UiAction)).GetCustomAttribute<McpServerToolAttribute>().Name);
    }

    public class RequestRecorder : DispatchProxy
    {
        public int Count;
        public string Method;
        public bool Mutation;
        public JsonElement Parameters;
        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            Assert.Equal(nameof(IRunOrchestrator.RequestAsync), method.Name);
            Count++;
            Method = (string)arguments[2];
            Parameters = JsonSerializer.SerializeToElement(arguments[3]);
            Mutation = (bool)arguments[4];
            return Task.FromResult<LiveTestResponse>(null);
        }
    }
}
