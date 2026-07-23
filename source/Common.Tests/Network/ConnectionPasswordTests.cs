using Common.Network;
using Xunit;

namespace Common.Tests.Network;

public class ConnectionPasswordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anything")]
    public void EmptyConfiguredPassword_AcceptsAnySuppliedPassword(string? suppliedPassword)
    {
        Assert.True(ConnectionPassword.IsAccepted(string.Empty, suppliedPassword));
    }

    [Fact]
    public void ConfiguredPassword_AcceptsExactMatch()
    {
        Assert.True(ConnectionPassword.IsAccepted("Secret", "Secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("secret")]
    [InlineData("Secret ")]
    public void ConfiguredPassword_RejectsMissingOrDifferentPassword(string? suppliedPassword)
    {
        Assert.False(ConnectionPassword.IsAccepted("Secret", suppliedPassword));
    }

    [Fact]
    public void IsValid_RejectsOnlyPasswordsOverLimit()
    {
        Assert.True(ConnectionPassword.IsValid(new string('x', ConnectionPassword.MaxLength)));
        Assert.False(ConnectionPassword.IsValid(new string('x', ConnectionPassword.MaxLength + 1)));
    }
}
