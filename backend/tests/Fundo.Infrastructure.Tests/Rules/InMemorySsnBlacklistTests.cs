using Fundo.Domain.Customers;
using Fundo.Infrastructure.Rules;

namespace Fundo.Infrastructure.Tests.Rules;

public class InMemorySsnBlacklistTests
{
    private readonly InMemorySsnBlacklist _blacklist = new(["999999999", "111111111"]);

    [Fact]
    public void Contains_WithFormattedEquivalentOfConfiguredSsn_ReturnsTrue()
    {
        Assert.True(_blacklist.Contains(Ssn.Create("999-99-9999")));
    }

    [Fact]
    public void Contains_WithUnlistedSsn_ReturnsFalse()
    {
        Assert.False(_blacklist.Contains(Ssn.Create("123-45-6789")));
    }

    [Fact]
    public void Constructor_WithInvalidConfiguredSsn_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new InMemorySsnBlacklist(["12345"]));
    }
}
