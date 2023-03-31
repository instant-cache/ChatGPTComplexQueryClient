using GPTAdScenarioGen;
using NLog;
using NLog.Web;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Info("Запуск приложения...");

var builder = WebApplication.CreateBuilder(args);


// NLog: Setup NLog for Dependency injection
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();
var loc = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location);
IConfiguration configuration = new ConfigurationBuilder().SetBasePath(loc)
                                                         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                                         .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                                                         .AddEnvironmentVariables()
                                                         .Build();

builder.Configuration.AddConfiguration(configuration);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<ChatGptApiService>();
builder.Services.AddSingleton<RequestLimiter>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app//.UseHttpsRedirection()
   .UseAuthorization()
   .UseWebSockets();
app.MapControllers();

logger.Info("Приложение запущено.");

app.Run();
