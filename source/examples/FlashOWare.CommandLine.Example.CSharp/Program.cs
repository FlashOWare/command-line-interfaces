using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using Octokit;

RootCommand rootCommand = new("C# sample app.");
Option<bool> option = new("--language", ["-l", "--lang"])
{
	Description = "Display the .NET language in use.",
	Arity = ArgumentArity.Zero,
	Action = new LanguageCommandLineAction(),
};
rootCommand.Options.Add(option);

Command command = new("repository", "Display GitHub repository information.");
Argument<string> argument = new("FULL-NAME")
{
	Description = """The full name of the repository in the form of "owner/repo".""",
	Arity = ArgumentArity.ZeroOrOne,
	DefaultValueFactory = static (ArgumentResult argumentResult) => "FlashOWare/command-line-interfaces",
};
argument.Validators.Add((ArgumentResult argumentResult) =>
{
	string value = argumentResult.GetRequiredValue(argument);

	int index = value.IndexOf('/');
	if (index == -1 || index != value.LastIndexOf('/') || index == 0 || index + 1 == value.Length)
	{
		argumentResult.AddError("""The full name of the repository must be in the form of "owner/repo".""");
	}
});
command.Arguments.Add(argument);
command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
	string fullName = parseResult.GetRequiredValue(argument);
	int index = fullName.IndexOf('/');
	string owner = fullName[..index];
	string repo = fullName[(index + 1)..];

	AssemblyName name = typeof(Program).Assembly.GetName();
	GitHubClient client = new(new ProductHeaderValue(name.Name, name.Version?.ToString()));
	Repository repository = await client.Repository.Get(owner, repo);
	RateLimit rateLimit = client.GetLastApiInfo().RateLimit;

	const string tabString = "    ";
	TextWriter output = parseResult.InvocationConfiguration.Output;

	await output.WriteLineAsync($"""
		Repository
		{tabString}Full Name: {repository.FullName}
		{tabString}Stargazers: {repository.StargazersCount}
		{tabString}Watchers: {repository.SubscribersCount}
		{tabString}Forks: {repository.ForksCount}
		{tabString}Open Issues: {repository.OpenIssuesCount}
		""");
	await output.WriteLineAsync($"""
		Rate Limiting
		{tabString}Requests per hour: {rateLimit.Limit - rateLimit.Remaining} / {rateLimit.Limit}
		{tabString}Window resets at: {rateLimit.Reset.LocalDateTime:yyyy-MM-dd HH:mm:ss.fffffff}
		""");

	return 0;
});
rootCommand.Subcommands.Add(command);

Directive directive = new("config")
{
	Description = "Show the command-line configuration that would have been used if the given command line were run.",
	Action = new ConfigCommandLineAction(),
};
rootCommand.Directives.Add(directive);

ParseResult parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

internal sealed class LanguageCommandLineAction : SynchronousCommandLineAction
{
	public override int Invoke(ParseResult parseResult)
	{
		TextWriter output = parseResult.InvocationConfiguration.Output;

		output.WriteLine("C#");

		return 0;
	}
}

internal sealed class ConfigCommandLineAction : SynchronousCommandLineAction
{
	public override int Invoke(ParseResult parseResult)
	{
		const string tabString = "    ";
		TextWriter output = parseResult.InvocationConfiguration.Output;

		output.WriteLine($"""
			Configuration
			{tabString}EnablePosixBundling: {parseResult.Configuration.EnablePosixBundling}
			""");

		output.WriteLine($"""
			Invocation
			{tabString}EnableDefaultExceptionHandler: {parseResult.InvocationConfiguration.EnableDefaultExceptionHandler}
			{tabString}ProcessTerminationTimeout: {(parseResult.InvocationConfiguration.ProcessTerminationTimeout is { } timeout ? timeout.ToString("c") : "<null>")}
			{tabString}Output: {parseResult.InvocationConfiguration.Output}
			{tabString}Error: {parseResult.InvocationConfiguration.Error}
			""");

		return 0;
	}
}
