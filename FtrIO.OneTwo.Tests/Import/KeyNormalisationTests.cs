using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Import;

public class KeyNormalisationTests
{
    [Fact]
    public void KebabCase_Converts_ToPascalCase()
    {
        KeyNormaliser.ToPascalCase("kebab-case").Should().Be("KebabCase");
    }

    [Fact]
    public void NewCheckoutFlow_Converts_Correctly()
    {
        KeyNormaliser.ToPascalCase("new-checkout-flow").Should().Be("NewCheckoutFlow");
    }

    [Fact]
    public void Underscores_AreLeft_Alone()
    {
        // Only hyphens are used as split points; underscores stay unchanged.
        // "already_pascal" has no hyphens so only the first char is capitalised.
        KeyNormaliser.ToPascalCase("already_pascal").Should().Be("Already_pascal");
    }

    [Fact]
    public void SingleWord_Capitalised()
    {
        KeyNormaliser.ToPascalCase("feature").Should().Be("Feature");
    }

    [Fact]
    public void EmptyString_Returns_EmptyString()
    {
        KeyNormaliser.ToPascalCase(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void AlreadyPascalCase_Preserved()
    {
        KeyNormaliser.ToPascalCase("MyFlag").Should().Be("MyFlag");
    }
}
