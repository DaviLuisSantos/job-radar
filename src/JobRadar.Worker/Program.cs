using JobRadar.Worker;
using JobRadar.Worker.Cli;

// "status seek:123 applied" and friends run once and exit; no arguments runs the daily worker.
var isCommand = CliCommand.IsCommand(args);
var hostArgs = isCommand ? [] : args;   // free text (notes) must not be read as configuration

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = hostArgs,
    // Config files next to the binary, not in the current directory (System32 under a Windows service).
    ContentRootPath = AppContext.BaseDirectory,
});
// Real profiles describe a person's plans and stay out of git (see appsettings.Local.example.json).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
// Environment and command line keep the last word, as in the default host.
builder.Configuration.AddEnvironmentVariables().AddCommandLine(hostArgs);
builder.Services.AddJobRadar(builder.Configuration);
// No-op unless started by the Service Control Manager; then logs also go to the Event Log.
builder.Services.AddWindowsService(o => o.ServiceName = "JobRadarWorker");

var host = builder.Build();
if (isCommand)
    return await CliRunner.RunAsync(args, host.Services, Console.Out);

await host.RunAsync();
return 0;
