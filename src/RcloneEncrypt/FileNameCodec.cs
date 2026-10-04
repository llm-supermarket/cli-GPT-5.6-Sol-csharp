using System.Security.Cryptography;

namespace RcloneEncrypt;

internal static class FileNameCodec
{
    private const string Base32HexAlphabet = "0123456789abcdefghijklmnopqrstuv";

    public static string Encode(ReadOnlySpan<byte> value, FileNameEncoding encoding) => encoding switch
    {
        FileNameEncoding.Base32 => EncodeBase32Hex(value),
        FileNameEncoding.Base64 => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    public static byte[] Decode(string value, FileNameEncoding encoding) => encoding switch
    {
        FileNameEncoding.Base32 => DecodeBase32Hex(value),
        FileNameEncoding.Base64 => DecodeBase64Url(value),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    private static string EncodeBase32Hex(ReadOnlySpan<byte> value)
    {
        var output = new char[(value.Length * 8 + 4) / 5];
        var accumulator = 0;
        var bits = 0;
        var outputIndex = 0;
        foreach (var item in value)
        {
            accumulator = (accumulator << 8) | item;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output[outputIndex++] = Base32HexAlphabet[(accumulator >> bits) & 31];
            }
        }

        if (bits > 0)
        {
            output[outputIndex] = Base32HexAlphabet[(accumulator << (5 - bits)) & 31];
        }

        return new string(output);
    }

    private static byte[] DecodeBase32Hex(string value)
    {
        if (value.EndsWith('=') || value.Length % 8 is 1 or 3 or 6)
        {
            throw new CryptographicException("Invalid base32 filename encoding.");
        }

        var output = new byte[value.Length * 5 / 8];
        var accumulator = 0;
        var bits = 0;
        var outputIndex = 0;
        foreach (var character in value)
        {
            var digit = Base32HexAlphabet.IndexOf(char.ToLowerInvariant(character));
            if (digit < 0)
            {
                throw new CryptographicException("Invalid base32 filename encoding.");
            }

            accumulator = (accumulator << 5) | digit;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output[outputIndex++] = (byte)(accumulator >> bits);
            }

            accumulator &= (1 << bits) - 1;
        }

        return output;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Invalid base64 filename encoding.", exception);
        }
    }
}
