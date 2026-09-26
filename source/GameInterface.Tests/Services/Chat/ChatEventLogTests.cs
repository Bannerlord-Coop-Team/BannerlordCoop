using GameInterface.Services.Chat;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatEventLogTests
{
    [Theory]
    [InlineData("", true, true, true)]
    [InlineData(ChatEventLog.DefaultCategory, true, true, true)]
    [InlineData("Social", false, false, true)]
    [InlineData(ChatEventLog.CombatCategory, true, false, true)]
    [InlineData(ChatEventLog.CombatCategory, false, true, false)]
    [InlineData(ChatEventLog.BarkCategory, false, true, true)]
    [InlineData(ChatEventLog.BarkCategory, true, false, false)]
    public void ShouldInclude_RespectsCombatAndBarkFilters(
        string category,
        bool reportDamage,
        bool reportBark,
        bool expected)
    {
        Assert.Equal(expected, ChatEventLog.ShouldInclude(category, reportDamage, reportBark));
    }

    [Fact]
    public void ShouldInclude_NullCategory_IsKept()
    {
        Assert.True(ChatEventLog.ShouldInclude(null, reportDamage: false, reportBark: false));
    }
}