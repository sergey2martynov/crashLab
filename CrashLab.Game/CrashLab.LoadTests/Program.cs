using CrashLab.LoadTests;
using NBomber.CSharp;

var httpClient = new HttpClient();
var testName = args.Length > 0 ? args[0] : "all";

switch (testName)
{
    case "table-isolation":
        await TableIsolationTest.RunAsync(httpClient);
        break;
    case "table-throughput":
        await TableThroughputTest.RunAsync(httpClient);
        break;
    case "peak-crash":
        await PeakCrashTest.RunAsync(httpClient);
        break;
    case "ws-listeners":
        var (latencies, chaosEvents) = await WsListenersTest.StartAsync();
        await Task.Delay(TimeSpan.FromMinutes(2));
        WsListenersTest.Report(latencies);
        WsListenersTest.ReportChaos(chaosEvents);
        break;
    case "cashout-crowd":
        NBomberRunner.RegisterScenarios(CashoutCrowdScenario.Create(httpClient)).Run();
        break;
    case "full-load":
        await FullLoadTest.RunAsync(httpClient);
        break;
    default: // "all" — прежнее поведение целиком
        var (l, _) = await WsListenersTest.StartAsync();
        await TableIsolationTest.RunAsync(httpClient);
        await PeakCrashTest.RunAsync(httpClient);
        await TableThroughputTest.RunAsync(httpClient);
        NBomberRunner.RegisterScenarios(CashoutCrowdScenario.Create(httpClient)).Run();
        await Task.Delay(TimeSpan.FromSeconds(30));
        WsListenersTest.Report(l);
        break;
}