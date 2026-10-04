using System.Text;
using Xunit;

namespace RcloneEncrypt.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"rclone-encrypt-tests-{Guid.NewGuid():N}");

    public CliApplicationTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task EncryptAndDecrypt_WithCommandLinePassword_RoundTripsFileAndWarns()
    {
        var source = CreateFile("plain.txt", "alpha bicycle canyon drift echo");
        var encrypted = Path.Combine(directory, "encrypted");
        var decrypted = Path.Combine(directory, "decrypted.txt");

        var encrypt = await RunAsync(["encrypt", "-i", source, "-o", encrypted, "--password", "secret"]);
        var decrypt = await RunAsync(["decrypt", "--input-file", encrypted, "--output-file", decrypted, "--password", "secret"]);

        Assert.Equal(0, encrypt.ExitCode);
        Assert.Equal(0, decrypt.ExitCode);
        Assert.Contains("process listings and shell history", encrypt.Error);
        Assert.Equal(await File.ReadAllTextAsync(source), await File.ReadAllTextAsync(decrypted));
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithSalt_RoundTripsMultipleBlocks()
    {
        var contents = string.Concat(Enumerable.Repeat("abandon ability able about above absent ", 4_000));
        var source = CreateFile("large.txt", contents);
        var encrypted = Path.Combine(directory, "large.enc");
        var decrypted = Path.Combine(directory, "large.out");

        Assert.Equal(0, (await RunAsync(["encrypt", "-i", source, "-o", encrypted, "--password", "secret", "--salt", "pepper"])).ExitCode);
        Assert.Equal(0, (await RunAsync(["decrypt", "-i", encrypted, "-o", decrypted, "--password", "secret", "--salt", "pepper"])).ExitCode);
        Assert.Equal(contents, await File.ReadAllTextAsync(decrypted));
    }

    [Fact]
    public async Task Base64FilenameEncoding_UsesRcloneCompatibleNameAndDecrypts()
    {
        var source = CreateFile("TEST_FILE BASE64.txt", "anchor brisk cloud delta ember");

        var encrypt = await RunAsync(["encrypt", "-i", source, "--password", "Testpassword1", "--filename-encoding", "base64"]);
        var encryptedPath = encrypt.Output.Trim();
        var decrypt = await RunAsync(["decrypt", "-i", encryptedPath, "--password", "Testpassword1", "--filename-encoding", "base64"]);

        Assert.Equal("Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY", Path.GetFileName(encryptedPath));
        Assert.Equal(0, decrypt.ExitCode);
        Assert.Equal("anchor brisk cloud delta ember", await File.ReadAllTextAsync(decrypt.Output.Trim()));
    }

    [Fact]
    public async Task PromptsForPasswordAndOptionalSalt()
    {
        var source = CreateFile("prompt.txt", "forest globe harbor island");
        var encrypted = Path.Combine(directory, "prompt.enc");
        var decrypted = Path.Combine(directory, "prompt.out");

        var encrypt = await RunAsync(["encrypt", "-i", source, "-o", encrypted], "secret\ncustom-salt\n");
        var decrypt = await RunAsync(["decrypt", "-i", encrypted, "-o", decrypted], "secret\ncustom-salt\n");

        Assert.Equal(0, encrypt.ExitCode);
        Assert.Contains("Password:", encrypt.Error);
        Assert.Contains("Salt (optional", encrypt.Error);
        Assert.Equal(0, decrypt.ExitCode);
        Assert.Equal(await File.ReadAllTextAsync(source), await File.ReadAllTextAsync(decrypted));
    }

    [Fact]
    public async Task EnvironmentPasswordAndEmptyPromptedSalt_RoundTrips()
    {
        var source = CreateFile("environment.txt", "jungle kingdom lemon meadow");
        var encrypted = Path.Combine(directory, "environment.enc");
        var decrypted = Path.Combine(directory, "environment.out");
        string? EnvironmentLookup(string name) => name == "RCLONE_ENCRYPT_PASSWORD" ? "secret" : null;

        var encrypt = await RunAsync(["encrypt", "-i", source, "-o", encrypted], "\n", EnvironmentLookup);
        var decrypt = await RunAsync(["decrypt", "-i", encrypted, "-o", decrypted], "\n", EnvironmentLookup);

        Assert.Equal(0, encrypt.ExitCode);
        Assert.DoesNotContain("Warning:", encrypt.Error);
        Assert.Equal(0, decrypt.ExitCode);
        Assert.Equal(await File.ReadAllTextAsync(source), await File.ReadAllTextAsync(decrypted));
    }

    [Theory]
    [InlineData("TEST_FILE.txt", "base32", "kr9tu4e1da4u3nifdd99g9tf5o")]
    [InlineData("TEST_FILE BASE64.txt", "base64", "Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY")]
    public void FilenameEncryption_MatchesRcloneVectors(string plainName, string encodingName, string encryptedName)
    {
        using var cipher = RcloneCipher.Create("Testpassword1", string.Empty);
        var encoding = encodingName == "base32" ? FileNameEncoding.Base32 : FileNameEncoding.Base64;

        Assert.Equal(encryptedName, cipher.EncryptFileName(plainName, encoding));
        Assert.Equal(plainName, cipher.DecryptFileName(encryptedName, encoding));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string CreateFile(string name, string contents)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    private static async Task<CliResult> RunAsync(
        string[] args,
        string standardInput = "",
        Func<string, string?>? environmentLookup = null)
    {
        using var input = new StringReader(standardInput);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(args, input, output, error, environmentLookup ?? (_ => null));
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliResult(int ExitCode, string Output, string Error);
}
