open System
open System.CommandLine
open System.CommandLine.Invocation
open System.CommandLine.Parsing
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Octokit

[<Sealed>]
type private LanguageCommandLineAction() =
    inherit SynchronousCommandLineAction()
    override this.Invoke(parseResult: ParseResult) =
        let output = parseResult.InvocationConfiguration.Output
        output.WriteLine("F#")
        0

[<Sealed>]
type private ConfigCommandLineAction() =
    inherit SynchronousCommandLineAction()
    override this.Invoke(parseResult: ParseResult) =
        let tabString = "    "
        let output = parseResult.InvocationConfiguration.Output

        output.WriteLine("Configuration")
        output.WriteLine($"{tabString}EnablePosixBundling: %b{parseResult.Configuration.EnablePosixBundling}")

        output.WriteLine("Invocation")
        output.WriteLine($"{tabString}EnableDefaultExceptionHandler: %b{parseResult.InvocationConfiguration.EnableDefaultExceptionHandler}")
        output.WriteLine($"""{tabString}ProcessTerminationTimeout: {(if parseResult.InvocationConfiguration.ProcessTerminationTimeout.HasValue then parseResult.InvocationConfiguration.ProcessTerminationTimeout.Value.ToString("c") else "<null>")}""")
        output.WriteLine($"{tabString}Output: {parseResult.InvocationConfiguration.Output}")
        output.WriteLine($"{tabString}Error: {parseResult.InvocationConfiguration.Error}")

        0

let rootCommand = RootCommand("F# sample app.")

let option = Option<bool>("--language", [| "-l"; "--lang" |],
    Description = "Display the .NET language in use.",
    Arity = ArgumentArity.Zero,
    Action = LanguageCommandLineAction()
)
rootCommand.Options.Add(option)

let command = Command("repository", "Display GitHub repository information.")
let argument = Argument<string>("FULL-NAME",
    Description = """The full name of the repository in the form of "owner/repo".""",
    Arity = ArgumentArity.ZeroOrOne,
    DefaultValueFactory = fun (argumentResult: ArgumentResult) -> "FlashOWare/command-line-interfaces"
)
argument.Validators.Add(fun (argumentResult : ArgumentResult) ->
    let value = argumentResult.GetRequiredValue(argument)

    let index = value.IndexOf('/')
    if index = -1 || index <> value.LastIndexOf('/') || index = 0 || index + 1 = value.Length then
        argumentResult.AddError("""The full name of the repository must be in the form of "owner/repo".""")
)
command.Arguments.Add(argument)
command.SetAction(fun (parseResult: ParseResult) (cancellationToken: CancellationToken) -> (task {
    let fullName = parseResult.GetRequiredValue(argument)
    let index = fullName.IndexOf('/')
    let owner = fullName.Substring(0, index)
    let repo = fullName.Substring(index + 1)

    let name = Assembly.GetExecutingAssembly().GetName()
    let client = GitHubClient(ProductHeaderValue(name.Name, match name.Version with | null -> null | version -> version.ToString()))
    let! repository = client.Repository.Get(owner, repo)
    let rateLimit = client.GetLastApiInfo().RateLimit

    let tabString = "    "
    let output = parseResult.InvocationConfiguration.Output

    do! output.WriteLineAsync("Repository")
    do! output.WriteLineAsync($"{tabString}Full Name: {repository.FullName}")
    do! output.WriteLineAsync($"{tabString}Stargazers: {repository.StargazersCount}")
    do! output.WriteLineAsync($"{tabString}Watchers: {repository.SubscribersCount}")
    do! output.WriteLineAsync($"{tabString}Forks: {repository.ForksCount}")
    do! output.WriteLineAsync($"{tabString}Open Issues: {repository.OpenIssuesCount}")

    do! output.WriteLineAsync("Rate Limiting")
    do! output.WriteLineAsync($"{tabString}Requests per hour: {rateLimit.Limit - rateLimit.Remaining} / {rateLimit.Limit}")
    do! output.WriteLineAsync($"""{tabString}Window resets at: {rateLimit.Reset.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}""")

    return 0
} : Task<int>))
rootCommand.Subcommands.Add(command)

let directive = Directive("config",
    Description = "Show the command-line configuration that would have been used if the given command line were run.",
    Action = ConfigCommandLineAction()
)
rootCommand.Directives.Add(directive)

let parseResult = rootCommand.Parse(Environment.GetCommandLineArgs() |> Array.skip 1)
let result = parseResult.Invoke()
exit result
