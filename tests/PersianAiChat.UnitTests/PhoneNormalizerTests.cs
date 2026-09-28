using FluentAssertions;
using PersianAiChat.Application.Validators;

namespace PersianAiChat.UnitTests;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("09122222222", "989122222222")]
    [InlineData("+989122222222", "989122222222")]
    [InlineData("989122222222", "989122222222")]
    [InlineData("09352345678", "989352345678")]
    [InlineData("09901234567", "989901234567")]
    public void TryNormalize_ValidInputs_ReturnsNormalized(string input, string expected)
    {
        var (success, normalized, error) = PhoneNormalizer.TryNormalize(input);
        success.Should().BeTrue(because: $"'{input}' is a valid Iranian mobile number");
        normalized.Should().Be(expected);
        error.Should().BeNull();
    }

    [Fact]
    public void TryNormalize_PersianDigits_ReturnsNormalized()
    {
        // ۰۹۱۲۲۲۲۲۲۲۲ in Persian
        var input = "۰۹۱۲۲۲۲۲۲۲۲";
        var (success, normalized, error) = PhoneNormalizer.TryNormalize(input);
        success.Should().BeTrue();
        normalized.Should().Be("989122222222");
    }

    [Fact]
    public void TryNormalize_ArabicDigits_ReturnsNormalized()
    {
        // ٠٩١٢٢٢٢٢٢٢٢ in Arabic-Indic
        var input = "٠٩١٢٢٢٢٢٢٢٢";
        var (success, normalized, error) = PhoneNormalizer.TryNormalize(input);
        success.Should().BeTrue();
        normalized.Should().Be("989122222222");
    }

    [Fact]
    public void TryNormalize_WithSpaces_ReturnsNormalized()
    {
        var (success, normalized, _) = PhoneNormalizer.TryNormalize("  09122222222  ");
        success.Should().BeTrue();
        normalized.Should().Be("989122222222");
    }

    [Theory]
    [InlineData("0912")]
    [InlineData("091222")]
    [InlineData("12345678901")]
    [InlineData("0912345678901")]
    [InlineData("abcdefghijk")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("00989122222222")]
    public void TryNormalize_InvalidInputs_ReturnsFailure(string? input)
    {
        var (success, normalized, error) = PhoneNormalizer.TryNormalize(input);
        success.Should().BeFalse(because: $"'{input}' is not a valid Iranian mobile number");
        normalized.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryNormalize_NullInput_ReturnsValidationMessage()
    {
        var (success, _, error) = PhoneNormalizer.TryNormalize(null);
        success.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryNormalize_LettersInPhone_ReturnsPersianError()
    {
        var (success, _, error) = PhoneNormalizer.TryNormalize("0912abc1234");
        success.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}
