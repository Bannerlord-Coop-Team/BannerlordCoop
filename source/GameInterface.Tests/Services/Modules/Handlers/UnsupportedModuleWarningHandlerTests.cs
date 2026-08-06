using GameInterface.Services.Modules;
using System.Collections.Generic;
using GameInterface.Services.Modules.Handlers;
using GameInterface.Services.UI.CoopOptions;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Modules.Handlers;
/// <summary>
/// Addition of multiple tests to check the logic behind the warning
/// </summary>
public class UnsupportedModuleWarningHandlerTests
{
    [Fact]
    public void OfficialModulesAndCoop_DoNotShowWarning()
    {
        var provider = new TestModuleInfoProvider(
            Module("Native", isOfficial: true),
            Module("SandBox", isOfficial: true),
            Module("StoryMode", isOfficial: true),
            Module("Coop", isOfficial: false));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});
        var shown = 0;

        coordinator.TryShowPrompt(true, _ => shown++);

        Assert.Equal(0, shown);
        Assert.Equal(1, provider.RequestCount);
    }

    [Fact]
    public void CommunityModules_AreListedInWarning()
    {
        var provider = new TestModuleInfoProvider(
            Module("Native", isOfficial: true),
            Module("Coop", isOfficial: false),
            Module("ExampleMod", isOfficial: false),
            Module("AnotherMod", isOfficial: false));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});
        InquiryData inquiry = null;

        coordinator.TryShowPrompt(true, value => inquiry = value);

        Assert.NotNull(inquiry);
        Assert.Equal(
            UnsupportedModuleWarningHandler.PromptTitle,
            inquiry.TitleText);
        Assert.Equal(
            "Bannerlord Coop may be unstable when used with additional modules. " +
            "The following active modules are not supported:\n\n" +
            "- AnotherMod\n" +
            "- ExampleMod\n\n" +
            "Continue at your own risk.",
            inquiry.Text);
    }

    [Fact]
    public void OfficialOptionalModule_ShowsWarning()
    {
        var provider = new TestModuleInfoProvider(
            Module("Native", isOfficial: true),
            Module("Coop", isOfficial: false),
            Module("NavalDLC", isOfficial: true, isDlc: true));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});
        InquiryData inquiry = null;

        coordinator.TryShowPrompt(true, value => inquiry = value);

        Assert.NotNull(inquiry);
        Assert.Contains("- NavalDLC", inquiry.Text);
    }

    [Fact]
    public void DedicatedServerModules_DoNotShowWarning()
    {
        var provider = new TestModuleInfoProvider(
            Module("Native", isOfficial: true),
            Module("Coop", isOfficial: false),
            Module("DedicatedServer.Linux", isOfficial: false),
            Module("DedicatedServer.Windows", isOfficial: false));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});
        var shown = 0;

        coordinator.TryShowPrompt(true, _ => shown++);

        Assert.Equal(0, shown);
    }

    [Fact]
    public void Prompt_IsNotShownUntilAllowedAndIsShownOnlyOnce()
    {
        var provider = new TestModuleInfoProvider(
            Module("ExampleMod", isOfficial: false));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});
        var shown = 0;

        coordinator.TryShowPrompt(false, _ => shown++);
        coordinator.TryShowPrompt(true, _ => shown++);
        coordinator.TryShowPrompt(true, _ => shown++);

        Assert.Equal(1, shown);
        Assert.Equal(1, provider.RequestCount);
    }

    [Fact]
    public void NoUnsupportedModules_AreEvaluatedOnlyOnce()
    {
        var provider = new TestModuleInfoProvider(
            Module("Native", isOfficial: true),
            Module("Coop", isOfficial: false));

        var coordinator = new UnsupportedModuleWarningHandler(provider, new TestOptionsStore(), _ => {});

        coordinator.TryShowPrompt(true, _ => { });
        coordinator.TryShowPrompt(true, _ => { });

        Assert.Equal(1, provider.RequestCount);
    }

    [Fact]
    public void Continue_DoesNotSuppressWarningOnNextLaunch()
    {
        var store = new TestOptionsStore();
        var provider = new TestModuleInfoProvider(
            Module("ExampleMod", isOfficial: false));
        InquiryData inquiry = null;

        var firstLaunchHandler = new UnsupportedModuleWarningHandler(
            provider,
            store,
            _ => { });
        firstLaunchHandler.TryShowPrompt(
            true,
            value => inquiry = value);
        
        Assert.NotNull(inquiry);

        // Continue does not save a suppression preference.
        inquiry.AffirmativeAction?.Invoke();

        var secondLaunchHandler = new UnsupportedModuleWarningHandler(
            provider,
            store, _ => { });
        var shownOnNextLaunch = 0;

        secondLaunchHandler.TryShowPrompt(
            true,
            _ => shownOnNextLaunch++);
        Assert.Equal(1, shownOnNextLaunch);
    }
    
    [Fact]
    public void DontShowAgain_SuppressesWarningOnNextLaunch()
    {
        var store = new TestOptionsStore();
        var provider = new TestModuleInfoProvider(
            Module("ExampleMod", isOfficial: false));
        InquiryData inquiry = null;

        var firstLaunchHandler = new UnsupportedModuleWarningHandler(
            provider,
            store,
            _ => { });
        firstLaunchHandler.TryShowPrompt(
            true,
            value => inquiry = value);

        Assert.NotNull(inquiry);
        Assert.NotNull(inquiry.NegativeAction);

        inquiry.NegativeAction();

        Assert.True(store.Options.TryGetSection(
            UnsupportedModuleWarningHandler.TabId,
            UnsupportedModuleWarningHandler.SectionId,
            out UnsupportedModuleWarningOptions savedOptions));
        Assert.True(savedOptions.DontShowAgain);
        
        var secondLaunchHandler = new UnsupportedModuleWarningHandler(
            provider,
            store,
            _ => { });
        var shownOnNextLaunch = 0;
        secondLaunchHandler.TryShowPrompt(
            true,
            _ => shownOnNextLaunch++);
        
        Assert.Equal(0, shownOnNextLaunch);
    }

    private static ModuleInfo Module(
        string id,
        bool isOfficial,
        bool isDlc = false)
    {
        return new ModuleInfo
        {
            Id = id,
            IsOfficial = isOfficial,
            IsDlc = isDlc,
        };
    }

    /// <summary>
    /// Supplies module information for unsupported module warning tests
    /// </summary>
    private sealed class TestModuleInfoProvider : IModuleInfoProvider
    {
        private readonly IEnumerable<ModuleInfo> modules;

        public TestModuleInfoProvider(params ModuleInfo[] modules)
        {
            this.modules = modules;
        }

        public int RequestCount { get; private set; }

        public IEnumerable<ModuleInfo> GetModuleInfos()
        {
            RequestCount++;
            return modules;
        }
    }
    
    /// <summary>
    /// Stores Coop options in memory for unsupported module warning tests
    /// </summary>
    private sealed class TestOptionsStore : ICoopOptionsStore
    {
        public TestOptionsStore(CoopOptionsData options = null)
        {
            Options = options ?? new CoopOptionsData();
        }
        public string FilePath => string.Empty;

        public CoopOptionsData Options { get; private set; }

        public bool TryLoad(out CoopOptionsData options)
        {
            options = Options;
            return true;
        }

        public CoopOptionsData LoadOrDefault()
        {
            return Options;
        }

        public void Save(CoopOptionsData options)
        {
            Options = options;
        }
    }
}

