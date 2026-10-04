namespace RcloneEncrypt;

internal static class Program
{
    public static async Task<int> Main(string[] args) =>
        await CliApplication.RunAsync(args, Console.In, Console.Out, Console.Error);
}
