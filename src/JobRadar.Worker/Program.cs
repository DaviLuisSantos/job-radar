using JobRadar.Worker;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // Config files next to the binary, not in the current directory (System32 under a Windows service).
    ContentRootPath = AppContext.BaseDirectory,
});
// Real profiles describe a person's plans and stay out of git (see appsettings.Local.example.json).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
// Environment and command line keep the last word, as in the default host.
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
builder.Services.AddJobRadar(builder.Configuration);

var host = builder.Build();
host.Run();
