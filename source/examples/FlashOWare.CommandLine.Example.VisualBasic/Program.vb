Imports System.CommandLine
Imports System.CommandLine.Invocation
Imports System.CommandLine.Parsing
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports Octokit

Friend Module Program
	Friend Function Main(args As String()) As Integer
		Dim rootCommand As New RootCommand("Visual Basic sample app.")

		Dim [option] As New [Option](Of Boolean)("--language", New String() {"-l", "--lang"}) With {
			.Description = "Display the .NET language in use.",
			.Arity = ArgumentArity.Zero,
			.Action = New LanguageCommandLineAction()
		}
		rootCommand.Options.Add([option])

		Dim command As New Command("repository", "Display GitHub repository information.")
		command.Aliases.Add("repo")
		Dim argument As New Argument(Of String)("FULL-NAME") With {
			.Description = "The full name of the repository in the form of ""owner/repo"".",
			.Arity = ArgumentArity.ZeroOrOne,
			.DefaultValueFactory = Function(argumentResult As ArgumentResult) "FlashOWare/command-line-interfaces"
		}
		argument.Validators.Add(
			Sub(argumentResult As ArgumentResult)
				Dim value As String = argumentResult.GetRequiredValue(argument)

				Dim index As Integer = value.IndexOf("/"c)
				If index = -1 OrElse index <> value.LastIndexOf("/"c) OrElse index = 0 OrElse index + 1 = value.Length Then
					argumentResult.AddError("The full name of the repository must be in the form of ""owner/repo"".")
				End If
			End Sub)
		command.Arguments.Add(argument)
		command.SetAction(
			Async Function(parseResult As ParseResult, cancellationToken As CancellationToken) As Task(Of Integer)
				Dim fullName As String = parseResult.GetRequiredValue(argument)
				Dim index As Integer = fullName.IndexOf("/"c)
				Dim owner As String = fullName.Substring(0, index)
				Dim repo As String = fullName.Substring(index + 1)

				Dim name As AssemblyName = GetType(Program).Assembly.GetName()
				Dim client As New GitHubClient(New ProductHeaderValue(name.Name, name.Version?.ToString()))
				Dim repository As Repository = Await client.Repository.Get(owner, repo)
				Dim rateLimit As RateLimit = client.GetLastApiInfo().RateLimit

				Const TabString As String = "    "
				Dim output As TextWriter = parseResult.InvocationConfiguration.Output

				Await output.WriteLineAsync("Repository")
				Await output.WriteLineAsync($"{TabString}Full Name: {repository.FullName}")
				Await output.WriteLineAsync($"{TabString}Stargazers: {repository.StargazersCount}")
				Await output.WriteLineAsync($"{TabString}Watchers: {repository.SubscribersCount}")
				Await output.WriteLineAsync($"{TabString}Forks: {repository.ForksCount}")
				Await output.WriteLineAsync($"{TabString}Open Issues: {repository.OpenIssuesCount}")

				Await output.WriteLineAsync("Rate Limiting")
				Await output.WriteLineAsync($"{TabString}Requests per hour: {rateLimit.Limit - rateLimit.Remaining} / {rateLimit.Limit}")
				Await output.WriteLineAsync($"{TabString}Window resets at: {rateLimit.Reset.LocalDateTime:yyyy-MM-dd HH:mm:ss.fffffff}")

				Return 0
			End Function)
		rootCommand.Subcommands.Add(command)

		Dim directive As New Directive("config") With {
			.Description = "Show the command-line configuration that would have been used if the given command line were run.",
			.Action = New ConfigCommandLineAction()
		}
		rootCommand.Directives.Add(directive)

		Dim result As ParseResult = rootCommand.Parse(args)
		Return result.Invoke()
	End Function
End Module

Friend NotInheritable Class LanguageCommandLineAction
	Inherits SynchronousCommandLineAction

	Public Overrides Function Invoke(parseResult As ParseResult) As Integer
		Dim output As TextWriter = parseResult.InvocationConfiguration.Output
		output.WriteLine("Visual Basic")
		Return 0
	End Function
End Class

Friend NotInheritable Class ConfigCommandLineAction
	Inherits SynchronousCommandLineAction

	Public Overrides Function Invoke(parseResult As ParseResult) As Integer
		Const TabString As String = "    "
		Dim output As TextWriter = parseResult.InvocationConfiguration.Output

		output.WriteLine("Configuration")
		output.WriteLine($"{TabString}EnablePosixBundling: {parseResult.Configuration.EnablePosixBundling}")

		output.WriteLine("Invocation")
		output.WriteLine($"{TabString}EnableDefaultExceptionHandler: {parseResult.InvocationConfiguration.EnableDefaultExceptionHandler}")
		output.WriteLine($"{TabString}ProcessTerminationTimeout: {If(parseResult.InvocationConfiguration.ProcessTerminationTimeout.HasValue,
			parseResult.InvocationConfiguration.ProcessTerminationTimeout.Value.ToString("c"),
			"<null>")}")
		output.WriteLine($"{TabString}Output: {parseResult.InvocationConfiguration.Output}")
		output.WriteLine($"{TabString}Error: {parseResult.InvocationConfiguration.Error}")

		Return 0
	End Function
End Class
