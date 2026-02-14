using ConcordIO.Tool.CliCommands;

using DotMake.CommandLine;

namespace ConcordIO.Tool;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		return await Cli.RunAsync<RootCommand>(args, new CliSettings { EnableDefaultExceptionHandler = true });
	}
}
