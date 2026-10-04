using System.Reflection;
using System.Security.Cryptography;

namespace RcloneEncrypt;

internal static class CliApplication
{
    private const string PasswordEnvironmentVariable = "RCLONE_ENCRYPT_PASSWORD";
    private const string SaltEnvironmentVariable = "RCLONE_ENCRYPT_SALT";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        Func<string, string?>? getEnvironmentVariable = null,
        CancellationToken cancellationToken = default)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        if (args is ["--version"] or ["-v"])
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                .Split('+')[0] ?? "dev";
            await output.WriteLineAsync($"cli-GPT-5.6-Sol-csharp {version}");
            return 0;
        }

        if (args.Count == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            await output.WriteAsync(Usage);
            return args.Count == 0 ? 1 : 0;
        }

        try
        {
            var options = Parse(args);
            var password = options.Password ?? getEnvironmentVariable(PasswordEnvironmentVariable);
            if (options.Password is not null)
            {
                await error.WriteLineAsync(
                    "Warning: --password exposes the password in process listings and shell history. " +
                    $"Prefer {PasswordEnvironmentVariable}, and remove this command from your terminal history.");
            }

            password ??= await PromptSecretAsync("Password: ", input, error);
            if (string.IsNullOrEmpty(password))
            {
                throw new CliException("A password is required.");
            }

            var salt = options.Salt ?? getEnvironmentVariable(SaltEnvironmentVariable);
            salt ??= await PromptSecretAsync("Salt (optional, press Enter for rclone default): ", input, error);

            using var cipher = RcloneCipher.Create(password, salt);
            var destination = options.OutputFile ?? GetDefaultOutputPath(options, cipher);

            if (PathsEqual(options.InputFile, destination))
            {
                throw new CliException("Input and output files must be different.");
            }

            if (options.Operation is Operation.Encrypt)
            {
                await cipher.EncryptFileAsync(options.InputFile, destination, cancellationToken);
            }
            else
            {
                await cipher.DecryptFileAsync(options.InputFile, destination, cancellationToken);
            }

            await output.WriteLineAsync(destination);
            return 0;
        }
        catch (CliException exception)
        {
            await error.WriteLineAsync($"Error: {exception.Message}");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            await error.WriteLineAsync($"Error: {exception.Message}");
            return 1;
        }
    }

    private static CliOptions Parse(IReadOnlyList<string> args)
    {
        var operation = args[0].ToLowerInvariant() switch
        {
            "encrypt" => Operation.Encrypt,
            "decrypt" => Operation.Decrypt,
            _ => throw new CliException("The first argument must be 'encrypt' or 'decrypt'.")
        };

        string? inputFile = null;
        string? outputFile = null;
        string? password = null;
        string? salt = null;
        var encoding = FileNameEncoding.Base32;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            string NextValue()
            {
                if (++index >= args.Count)
                {
                    throw new CliException($"Missing value for {option}.");
                }

                return args[index];
            }

            switch (option)
            {
                case "-i" or "--input-file":
                    inputFile = NextValue();
                    break;
                case "-o" or "--output-file":
                    outputFile = NextValue();
                    break;
                case "--password":
                    password = NextValue();
                    break;
                case "--salt":
                    salt = NextValue();
                    break;
                case "--filename-encoding":
                    encoding = NextValue().ToLowerInvariant() switch
                    {
                        "base32" => FileNameEncoding.Base32,
                        "base64" => FileNameEncoding.Base64,
                        _ => throw new CliException("Filename encoding must be 'base32' or 'base64'.")
                    };
                    break;
                default:
                    throw new CliException($"Unknown option: {option}.");
            }
        }

        if (string.IsNullOrWhiteSpace(inputFile))
        {
            throw new CliException("An input file is required with -i or --input-file.");
        }

        return new CliOptions(operation, inputFile, outputFile, password, salt, encoding);
    }

    private static async Task<string> PromptSecretAsync(string prompt, TextReader input, TextWriter error)
    {
        await error.WriteAsync(prompt);

        if (!ReferenceEquals(input, Console.In) || Console.IsInputRedirected)
        {
            return await input.ReadLineAsync() ?? string.Empty;
        }

        var value = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter)
            {
                await error.WriteLineAsync();
                return new string([.. value]);
            }

            if (key.Key is ConsoleKey.Backspace)
            {
                if (value.Count > 0)
                {
                    value.RemoveAt(value.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                value.Add(key.KeyChar);
            }
        }
    }

    private static string GetDefaultOutputPath(CliOptions options, RcloneCipher cipher)
    {
        var fullPath = Path.GetFullPath(options.InputFile);
        var directory = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);
        var transformed = options.Operation is Operation.Encrypt
            ? cipher.EncryptFileName(fileName, options.Encoding)
            : cipher.DecryptFileName(fileName, options.Encoding);
        return Path.Combine(directory, transformed);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private const string Usage =
        """
        rclone-compatible file encryption and decryption

        Usage:
          cli-GPT-5.6-Sol-csharp encrypt -i <file> [-o <file>] [options]
          cli-GPT-5.6-Sol-csharp decrypt -i <file> [-o <file>] [options]

        Options:
          -i, --input-file <file>          Input file (required)
          -o, --output-file <file>         Output file (default: transformed input filename)
              --password <password>        Password (unsafe; prefer RCLONE_ENCRYPT_PASSWORD)
              --salt <salt>                Optional salt (or RCLONE_ENCRYPT_SALT)
              --filename-encoding <value>  base32 (default) or base64
          -h, --help                       Show help
          -v, --version                    Show version

        Without password/salt options or environment variables, the CLI prompts securely.
        """;

    private sealed class CliException(string message) : Exception(message);
}

internal sealed record CliOptions(
    Operation Operation,
    string InputFile,
    string? OutputFile,
    string? Password,
    string? Salt,
    FileNameEncoding Encoding);

internal enum Operation
{
    Encrypt,
    Decrypt
}

internal enum FileNameEncoding
{
    Base32,
    Base64
}
