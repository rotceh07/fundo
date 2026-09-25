using Fundo.Domain.Customers;

namespace Fundo.Domain.Tests.Customers;

public class SsnTests
{
    [Fact]
    public void Create_WithFormattedSsn_NormalizesValue()
    {
        var ssn = Ssn.Create("123-45-6789");

        Assert.Equal("123456789", ssn.Value);
    }

    [Fact]
    public void Create_WithUnformattedSsn_KeepsValue()
    {
        var ssn = Ssn.Create("123456789");

        Assert.Equal("123456789", ssn.Value);
    }

    [Theory]
    [InlineData("123 45 6789")]
    [InlineData("  123-45-6789  ")]
    public void Create_WithSpaces_NormalizesValue(string input)
    {
        var ssn = Ssn.Create(input);

        Assert.Equal("123456789", ssn.Value);
    }

    [Fact]
    public void Create_WithDifferentFormatsOfSameNumber_ProducesEqualInstances()
    {
        var formatted = Ssn.Create("123-45-6789");
        var unformatted = Ssn.Create("123456789");

        Assert.Equal(formatted, unformatted);
    }

    [Fact]
    public void ToString_ReturnsCanonicalValue()
    {
        var ssn = Ssn.Create("123-45-6789");

        Assert.Equal("123456789", ssn.ToString());
    }

    [Fact]
    public void Create_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Ssn.Create(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespace_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => Ssn.Create(input));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    public void Create_WithWrongNumberOfDigits_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => Ssn.Create(input));
    }

    [Theory]
    [InlineData("123-45-ABCD")]
    [InlineData("١٢٣٤٥٦٧٨٩")]
    public void Create_WithNonAsciiDigitCharacters_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => Ssn.Create(input));
    }
}
