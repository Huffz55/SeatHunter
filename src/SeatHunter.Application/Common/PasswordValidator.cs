namespace SeatHunter.Application.Common;

public static class PasswordValidator
{
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Şifre en az 8 karakter uzunluğunda olmalıdır.");

        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Şifre en az bir büyük harf (A-Z) içermelidir.");

        if (!password.Any(char.IsLower))
            throw new ArgumentException("Şifre en az bir küçük harf (a-z) içermelidir.");

        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Şifre en az bir rakam (0-9) içermelidir.");
    }
}
