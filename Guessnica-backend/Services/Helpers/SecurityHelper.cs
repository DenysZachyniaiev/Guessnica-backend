namespace Guessnica_backend.Services.Helpers;

using System;
using System.Security.Cryptography;
using System.Text;

public static class SecurityHelper
{
    public static string HashCode(string code, Guid salt)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{salt}:{code}");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
