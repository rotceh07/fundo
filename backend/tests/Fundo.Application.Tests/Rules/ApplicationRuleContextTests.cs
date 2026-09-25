using Fundo.Application.Rules;
using Fundo.Domain.Customers;

namespace Fundo.Application.Tests.Rules;

public class ApplicationRuleContextTests
{
    private static readonly Ssn DefaultSsn = Ssn.Create("123-45-6789");

    [Fact]
    public void Create_WithValidData_KeepsStateAndSsn()
    {
        var context = ApplicationRuleContext.Create("FL", DefaultSsn);

        Assert.Equal("FL", context.State);
        Assert.Equal(DefaultSsn, context.Ssn);
    }

    [Theory]
    [InlineData("ny")]
    [InlineData("NY")]
    [InlineData(" Ny ")]
    [InlineData("nY")]
    public void Create_WithDifferentCasingOrSpacing_NormalizesState(string state)
    {
        var context = ApplicationRuleContext.Create(state, DefaultSsn);

        Assert.Equal("NY", context.State);
    }

    [Fact]
    public void Create_WithNullState_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationRuleContext.Create(null!, DefaultSsn));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceState_ThrowsArgumentException(string state)
    {
        Assert.Throws<ArgumentException>(() => ApplicationRuleContext.Create(state, DefaultSsn));
    }

    [Fact]
    public void Create_WithNullSsn_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationRuleContext.Create("FL", null!));
    }
}
