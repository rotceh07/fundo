using Fundo.Application.Applications.Events;
using Fundo.Application.Applications.Submit;

namespace Fundo.Application.Tests.Applications;

public class SsnRedactionTests
{
    [Fact]
    public void SubmitApplicationCommand_ToString_DoesNotExposeSsn()
    {
        var command = new SubmitApplicationCommand(
            "John", "Doe", "100 Main St", "FL", "Acme", 10_000m, "123456789");

        var text = command.ToString();

        Assert.DoesNotContain("123456789", text);
        Assert.Contains("***", text);
    }

    [Fact]
    public void ApplicationApprovedEvent_ToString_DoesNotExposeSsn()
    {
        var applicationEvent = new ApplicationApprovedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "John",
            "Doe",
            "100 Main St",
            "FL",
            "Acme",
            "123456789",
            10_000m,
            ApplicationEventOperation.Create);

        var text = applicationEvent.ToString();

        Assert.DoesNotContain("123456789", text);
        Assert.Contains("***", text);
    }
}
