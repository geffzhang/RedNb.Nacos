using System.Text.Json;
using RedNb.Nacos.Sample.Aot;

Console.WriteLine($"MODE dynamic={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported} reflection={JsonSerializer.IsReflectionEnabledByDefault}");
LiveChecks.CheckValues();
await LiveChecks.RunAsync(args.Contains("--recovery"));
Console.WriteLine("PASS live SDK contracts (NuGet consumer)");
