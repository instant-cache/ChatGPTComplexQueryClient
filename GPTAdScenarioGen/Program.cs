using GPTAdScenarioGen;
using NLog;
using NLog.Web;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Info("������ ����������...");

var builder = WebApplication.CreateBuilder(args);

IConfiguration configuration = new ConfigurationBuilder().SetBasePath(builder.Environment.ContentRootPath)
                                                         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                                         .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                                                         .Build();

builder.Configuration.AddConfiguration(configuration);

// NLog: Setup NLog for Dependency injection
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();
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

logger.Info("���������� ��������.");

app.Run();
