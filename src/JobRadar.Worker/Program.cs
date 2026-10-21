using JobRadar.Worker;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // Config files next to the binary, not in the current directory (System32 under a Windows service).
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddJobRadar(builder.Configuration);

var host = builder.Build();
host.Run();
