using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>
/// Tests campaign map bug-report eligibility. 
/// </summary>
public class EscapeMenuCoopOptionsPatchTests
{
    [Fact]
    public void CanOpenBugReport_WhenAvailableAndIdle_ReturnsTrue()
    {
        bool result = EscapeMenuCoopOptionsPatch.CanOpenBugReport(
            isAvailable: true,
            isConversationInProgress: false);
        
        Assert.True(result);
    }

    [Fact]
    public void CanOpenBugReport_DuringConversation_ReturnsFalse()
    {
        bool result = EscapeMenuCoopOptionsPatch.CanOpenBugReport(
            isAvailable: true,
            isConversationInProgress: true);
        
        Assert.False(result);
    }

    [Fact]
    public void CanOpenBugReport_WhenUnavailable_ReturnsFalse()
    {
        bool result = EscapeMenuCoopOptionsPatch.CanOpenBugReport(
            isAvailable: false,
            isConversationInProgress: false);
        
        Assert.False(result);
    }
}