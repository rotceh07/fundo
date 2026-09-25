using Fundo.Application.Applications.Submit;

namespace Fundo.Application.Tests.Applications.Submit;

public class SubmitApplicationResultTests
{
    [Fact]
    public void Approved_WithApplicationId_ReturnsApprovedResult()
    {
        var applicationId = Guid.NewGuid();

        var result = SubmitApplicationResult.Approved(applicationId);

        Assert.True(result.IsApproved);
        Assert.Equal(applicationId, result.ApplicationId);
        Assert.Null(result.DenialReason);
    }

    [Fact]
    public void Approved_WithEmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SubmitApplicationResult.Approved(Guid.Empty));
    }

    [Fact]
    public void Denied_WithReason_ReturnsDeniedResult()
    {
        var result = SubmitApplicationResult.Denied("Not eligible.");

        Assert.False(result.IsApproved);
        Assert.Null(result.ApplicationId);
        Assert.Equal("Not eligible.", result.DenialReason);
    }

    [Fact]
    public void Denied_WithNullReason_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SubmitApplicationResult.Denied(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Denied_WithEmptyReason_ThrowsArgumentException(string reason)
    {
        Assert.Throws<ArgumentException>(() => SubmitApplicationResult.Denied(reason));
    }
}
