using System.Security.Cryptography;

namespace RcloneEncrypt;

internal static class Eme
{
    private const int BlockSize = 16;

    public static byte[] Transform(byte[] key, byte[] tweak, byte[] input, bool encrypt)
    {
        var blockCount = input.Length / BlockSize;
        if (tweak.Length != BlockSize || input.Length == 0 || input.Length % BlockSize != 0 || blockCount > 128)
        {
            throw new CryptographicException("Invalid EME input.");
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var lTable = TabulateL(aes, blockCount);
        var result = new byte[input.Length];
        Span<byte> temporary = stackalloc byte[BlockSize];

        for (var block = 0; block < blockCount; block++)
        {
            Xor(temporary, input.AsSpan(block * BlockSize, BlockSize), lTable[block]);
            AesTransform(aes, temporary, result.AsSpan(block * BlockSize, BlockSize), encrypt);
        }

        Span<byte> mp = stackalloc byte[BlockSize];
        Xor(mp, result.AsSpan(0, BlockSize), tweak);
        for (var block = 1; block < blockCount; block++)
        {
            Xor(mp, mp, result.AsSpan(block * BlockSize, BlockSize));
        }

        Span<byte> mc = stackalloc byte[BlockSize];
        AesTransform(aes, mp, mc, encrypt);
        Span<byte> m = stackalloc byte[BlockSize];
        Xor(m, mp, mc);

        for (var block = 1; block < blockCount; block++)
        {
            MultiplyByTwo(m, m);
            Xor(temporary, result.AsSpan(block * BlockSize, BlockSize), m);
            temporary.CopyTo(result.AsSpan(block * BlockSize, BlockSize));
        }

        Span<byte> first = stackalloc byte[BlockSize];
        Xor(first, mc, tweak);
        for (var block = 1; block < blockCount; block++)
        {
            Xor(first, first, result.AsSpan(block * BlockSize, BlockSize));
        }

        first.CopyTo(result);
        for (var block = 0; block < blockCount; block++)
        {
            var destination = result.AsSpan(block * BlockSize, BlockSize);
            AesTransform(aes, destination, temporary, encrypt);
            Xor(destination, temporary, lTable[block]);
        }

        return result;
    }

    private static byte[][] TabulateL(Aes aes, int count)
    {
        var encryptedZero = new byte[BlockSize];
        aes.EncryptEcb(new byte[BlockSize], encryptedZero, PaddingMode.None);
        var table = new byte[count][];
        for (var index = 0; index < count; index++)
        {
            table[index] = new byte[BlockSize];
            MultiplyByTwo(table[index], encryptedZero);
            table[index].CopyTo(encryptedZero, 0);
        }

        return table;
    }

    private static void AesTransform(Aes aes, ReadOnlySpan<byte> input, Span<byte> output, bool encrypt)
    {
        if (encrypt)
        {
            aes.EncryptEcb(input, output, PaddingMode.None);
        }
        else
        {
            aes.DecryptEcb(input, output, PaddingMode.None);
        }
    }

    private static void MultiplyByTwo(Span<byte> output, ReadOnlySpan<byte> input)
    {
        Span<byte> temporary = stackalloc byte[BlockSize];
        temporary[0] = (byte)((input[0] << 1) ^ (0x87 & -(input[^1] >> 7)));
        for (var index = 1; index < BlockSize; index++)
        {
            temporary[index] = (byte)((input[index] << 1) + (input[index - 1] >> 7));
        }

        temporary.CopyTo(output);
    }

    private static void Xor(Span<byte> output, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        for (var index = 0; index < left.Length; index++)
        {
            output[index] = (byte)(left[index] ^ right[index]);
        }
    }
}
