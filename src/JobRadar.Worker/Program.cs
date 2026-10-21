using JobRadar.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddJobRadar(builder.Configuration);

var host = builder.Build();
host.Run();
