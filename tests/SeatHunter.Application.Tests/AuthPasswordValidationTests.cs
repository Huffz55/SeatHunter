using FluentAssertions;
using SeatHunter.Application.Common;

namespace SeatHunter.Application.Tests;

public class AuthPasswordValidationTests
{
    [Theory]
    [InlineData("short")]           // < 8 karakter
    [InlineData("alllowercase123")] // Büyük harf yok
    [InlineData("ALLUPPERCASE123")] // Küçük harf yok
    [InlineData("NoDigitsHere!")]   // Rakam yok
    [InlineData("")]                // Boş
    [InlineData("   ")]             // Boşluk
    public void ValidatePasswordStrength_Should_ThrowArgumentException_WhenPasswordIsWeak(string weakPassword)
    {
        Action act = () => PasswordValidator.Validate(weakPassword);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("SecureP@ss123")]
    [InlineData("StrongPassword1")]
    [InlineData("Tarkan2026!")]
    public void ValidatePasswordStrength_Should_NotThrow_WhenPasswordIsStrong(string strongPassword)
    {
        Action act = () => PasswordValidator.Validate(strongPassword);

        act.Should().NotThrow();
    }
}
