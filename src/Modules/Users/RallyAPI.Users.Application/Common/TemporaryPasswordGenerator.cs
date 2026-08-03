using System.Security.Cryptography;

namespace RallyAPI.Users.Application.Common;

internal static class TemporaryPasswordGenerator
{
    // Excludes visually ambiguous characters (0/O, 1/I/l) since this is read aloud/typed by support staff.
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int Length = 14;

    public static string Generate()
    {
        var password = new char[Length];
        var buffer = new byte[1];

        for (var i = 0; i < Length; i++)
        {
            RandomNumberGenerator.Fill(buffer);
            password[i] = Chars[buffer[0] % Chars.Length];
        }

        return new string(password);
    }
}
