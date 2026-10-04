using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace RcloneEncrypt;

internal sealed class RcloneCipher : IDisposable
{
    private const int DataKeySize = 32;
    private const int NameKeySize = 32;
    private const int NameTweakSize = 16;
    private const int NonceSize = 24;
    private const int BlockDataSize = 64 * 1024;
    private const int MacSize = 16;
    private static readonly byte[] Magic = "RCLONE\0\0"u8.ToArray();
    private static readonly byte[] DefaultSalt =
        [0xA8, 0x0D, 0xF4, 0x3A, 0x8F, 0xBD, 0x03, 0x08, 0xA7, 0xCA, 0xB8, 0x3E, 0x58, 0x1F, 0x86, 0xB1];

    private readonly byte[] dataKey;
    private readonly byte[] nameKey;
    private readonly byte[] nameTweak;
    private bool disposed;

    private RcloneCipher(byte[] keyMaterial)
    {
        dataKey = keyMaterial[..DataKeySize];
        nameKey = keyMaterial[DataKeySize..(DataKeySize + NameKeySize)];
        nameTweak = keyMaterial[(DataKeySize + NameKeySize)..];
        CryptographicOperations.ZeroMemory(keyMaterial);
    }

    public static RcloneCipher Create(string password, string salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltBytes = string.IsNullOrEmpty(salt) ? DefaultSalt : Encoding.UTF8.GetBytes(salt);
        try
        {
            return new RcloneCipher(SCrypt.Generate(
                passwordBytes,
                saltBytes,
                16_384,
                8,
                1,
                DataKeySize + NameKeySize + NameTweakSize));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (!ReferenceEquals(saltBytes, DefaultSalt))
            {
                CryptographicOperations.ZeroMemory(saltBytes);
            }
        }
    }

    public async Task EncryptFileAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        await TransformFileAsync(inputPath, outputPath, async (input, output) =>
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            await output.WriteAsync(Magic, cancellationToken);
            await output.WriteAsync(nonce, cancellationToken);

            var plaintext = ArrayPool<byte>.Shared.Rent(BlockDataSize);
            try
            {
                while (true)
                {
                    var count = await ReadBlockAsync(input, plaintext.AsMemory(0, BlockDataSize), cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }

                    var ciphertext = SecretBox.Seal(plaintext.AsSpan(0, count), nonce, dataKey);
                    await output.WriteAsync(ciphertext, cancellationToken);
                    IncrementNonce(nonce);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                ArrayPool<byte>.Shared.Return(plaintext);
                CryptographicOperations.ZeroMemory(nonce);
            }
        }, cancellationToken);
    }

    public async Task DecryptFileAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        await TransformFileAsync(inputPath, outputPath, async (input, output) =>
        {
            var header = new byte[Magic.Length + NonceSize];
            if (await ReadBlockAsync(input, header, cancellationToken) != header.Length)
            {
                throw new CryptographicException("The input is too short to be an rclone encrypted file.");
            }

            if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                throw new CryptographicException("The input does not have an rclone encrypted-file header.");
            }

            var nonce = header[Magic.Length..];
            var ciphertext = ArrayPool<byte>.Shared.Rent(BlockDataSize + MacSize);
            try
            {
                while (true)
                {
                    var count = await ReadBlockAsync(input, ciphertext.AsMemory(0, BlockDataSize + MacSize), cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }

                    if (count <= MacSize)
                    {
                        throw new CryptographicException("The encrypted file has a truncated block.");
                    }

                    var plaintext = SecretBox.Open(ciphertext.AsSpan(0, count), nonce, dataKey);
                    await output.WriteAsync(plaintext, cancellationToken);
                    CryptographicOperations.ZeroMemory(plaintext);
                    IncrementNonce(nonce);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ciphertext);
                ArrayPool<byte>.Shared.Return(ciphertext);
                CryptographicOperations.ZeroMemory(nonce);
            }
        }, cancellationToken);
    }

    public string EncryptFileName(string fileName, FileNameEncoding encoding)
    {
        var plain = Encoding.UTF8.GetBytes(fileName);
        var paddingLength = 16 - (plain.Length % 16);
        var padded = new byte[plain.Length + paddingLength];
        plain.CopyTo(padded, 0);
        padded.AsSpan(plain.Length).Fill((byte)paddingLength);
        var encrypted = Eme.Transform(nameKey, nameTweak, padded, encrypt: true);
        return FileNameCodec.Encode(encrypted, encoding);
    }

    public string DecryptFileName(string fileName, FileNameEncoding encoding)
    {
        var encrypted = FileNameCodec.Decode(fileName, encoding);
        if (encrypted.Length == 0 || encrypted.Length % 16 != 0 || encrypted.Length > 2048)
        {
            throw new CryptographicException("The encrypted filename has an invalid length.");
        }

        var padded = Eme.Transform(nameKey, nameTweak, encrypted, encrypt: false);
        var paddingLength = padded[^1];
        if (paddingLength is 0 or > 16 || paddingLength > padded.Length ||
            padded.AsSpan(padded.Length - paddingLength).ContainsAnyExcept(paddingLength))
        {
            throw new CryptographicException("The encrypted filename has invalid padding (wrong password, salt, or encoding).");
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(padded.AsSpan(0, padded.Length - paddingLength));
        }
        catch (DecoderFallbackException exception)
        {
            throw new CryptographicException("The decrypted filename is not valid UTF-8.", exception);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(dataKey);
        CryptographicOperations.ZeroMemory(nameKey);
        CryptographicOperations.ZeroMemory(nameTweak);
        disposed = true;
    }

    private static async Task TransformFileAsync(
        string inputPath,
        string outputPath,
        Func<FileStream, FileStream, Task> transform,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            throw new IOException($"Input file not found: {inputPath}");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        var temporaryPath = fullOutputPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true))
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65_536, true))
            {
                await transform(input, output);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static async Task<int> ReadBlockAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[total..], cancellationToken);
            if (count == 0)
            {
                break;
            }

            total += count;
        }

        return total;
    }

    private static void IncrementNonce(Span<byte> nonce)
    {
        for (var index = 0; index < nonce.Length; index++)
        {
            nonce[index]++;
            if (nonce[index] != 0)
            {
                break;
            }
        }
    }
}

internal static class SecretBox
{
    private const int KeyStreamPrefixSize = 32;
    private const int MacSize = 16;

    public static byte[] Seal(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        var cipherInput = new byte[KeyStreamPrefixSize + plaintext.Length];
        plaintext.CopyTo(cipherInput.AsSpan(KeyStreamPrefixSize));
        var cipherOutput = ApplyXsalsa20(cipherInput, nonce, key);
        var output = new byte[MacSize + plaintext.Length];
        cipherOutput.AsSpan(KeyStreamPrefixSize).CopyTo(output.AsSpan(MacSize));
        ComputeMac(cipherOutput.AsSpan(0, KeyStreamPrefixSize), output.AsSpan(MacSize), output.AsSpan(0, MacSize));
        CryptographicOperations.ZeroMemory(cipherInput);
        CryptographicOperations.ZeroMemory(cipherOutput);
        return output;
    }

    public static byte[] Open(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        var cipherBody = ciphertext[MacSize..];
        var stream = ApplyXsalsa20(new byte[KeyStreamPrefixSize], nonce, key);
        Span<byte> expectedMac = stackalloc byte[MacSize];
        ComputeMac(stream, cipherBody, expectedMac);
        if (!CryptographicOperations.FixedTimeEquals(expectedMac, ciphertext[..MacSize]))
        {
            throw new CryptographicException("Failed to authenticate encrypted block (wrong password or salt).");
        }

        var cipherInput = new byte[KeyStreamPrefixSize + cipherBody.Length];
        cipherBody.CopyTo(cipherInput.AsSpan(KeyStreamPrefixSize));
        var plainWithPrefix = ApplyXsalsa20(cipherInput, nonce, key);
        var plaintext = plainWithPrefix[KeyStreamPrefixSize..];
        CryptographicOperations.ZeroMemory(cipherInput);
        CryptographicOperations.ZeroMemory(plainWithPrefix);
        CryptographicOperations.ZeroMemory(stream);
        return plaintext;
    }

    private static byte[] ApplyXsalsa20(ReadOnlySpan<byte> input, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        var engine = new XSalsa20Engine();
        engine.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(key), nonce));
        var output = new byte[input.Length];
        engine.ProcessBytes(input, output);
        return output;
    }

    private static void ComputeMac(ReadOnlySpan<byte> polyKey, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        var mac = new Poly1305();
        mac.Init(new KeyParameter(polyKey));
        mac.BlockUpdate(data);
        mac.DoFinal(destination);
    }
}
