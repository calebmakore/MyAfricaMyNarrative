using MyAfricaMyNarrative.Infrastructure;
using MyAfricaMyNarrative.Infrastructure.Jobs;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddInfrastructure(builder.Configuration);
// Optional extra capacity: the web app already processes exports unless Jobs:ProcessInWeb is false.
builder.Services.AddHostedService<ExportJobProcessor>();

var host = builder.Build();
host.Run();
