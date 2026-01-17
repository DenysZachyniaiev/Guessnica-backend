namespace Guessnica_backend.Services.Helpers;

using System;
using System.Security.Cryptography;

public static class CodeGenerator
{
    public static string Generate6DigitCode()
    {
        Span<byte> b = stackalloc byte[4];
        RandomNumberGenerator.Fill(b);
        uint val = BitConverter.ToUInt32(b);
        return (val % 1_000_000u).ToString("D6");
    }
}
