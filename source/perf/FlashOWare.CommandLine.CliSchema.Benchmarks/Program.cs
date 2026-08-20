using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

#if DEBUG
IConfig config = new DebugInProcessConfig();
#else
IConfig config = ManualConfig.Create(DefaultConfig.Instance)
	.WithOptions(ConfigOptions.DisableLogFile)
	.AddDiagnoser(MemoryDiagnoser.Default);
#endif

IEnumerable<Summary> summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

Console.WriteLine("Summaries:");
foreach (Summary summary in summaries)
{
	Console.WriteLine($"- Ran {summary.Title} with {summary.ValidationErrors.Length} validation errors.");
}
