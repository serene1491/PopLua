using SplitHost.App.Scripting;

var host = ScriptHost.CreateDefault();
var result = await host.Run("welcome", "Welcome Script");

Console.WriteLine(result.Ok ? $"result: {result.Unwrap()}" : result.Error);
host.PrintLog();
host.PrintDiagnostics();
