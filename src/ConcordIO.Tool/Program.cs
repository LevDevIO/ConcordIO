using DotMake.CommandLine;
using ConcordIO.Tool.CliCommands;

namespace ConcordIO.Tool;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await Cli.RunAsync<RootCommand>(args, new CliSettings { EnableDefaultExceptionHandler = true });
    }
}
