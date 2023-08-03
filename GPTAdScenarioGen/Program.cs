using GPTAdScenarioGen;
using NLog;
using NLog.Web;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Info("Запуск приложения...");

//Настройка пути к Webroot
var appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions()
    {
        ContentRootPath = appPath,
        WebRootPath = appPath + Path.DirectorySeparatorChar + "wwwroot",
        Args = args,
    });
logger.Debug("contentroot = {path}", builder.Environment.ContentRootPath);
logger.Debug("webroot = {path}", builder.Environment.WebRootPath);

// NLog: Setup NLog for Dependency injection
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();
IConfiguration configuration = new ConfigurationBuilder().SetBasePath(appPath)
                                                         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                                         .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                                                         .AddEnvironmentVariables()
                                                         .Build();

builder.Configuration.AddConfiguration(configuration);

//CORS
string[] allowedOrigins = null;
var allowedOriginsConfig = builder.Configuration.GetSection("AllowedOrigins");
if (allowedOriginsConfig != null)
{
    allowedOrigins = allowedOriginsConfig.Get<string[]>();
}
else
{
    allowedOriginsConfig = builder.Configuration.GetSection("AllowedHosts");
    allowedOrigins = allowedOriginsConfig.Value.Split(';');
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppsettingsPolicy", builder =>
    {
        if (allowedOrigins != null && allowedOrigins.Length > 0)
            builder.WithOrigins(allowedOrigins);
        else
            builder.AllowAnyOrigin();
        builder.AllowAnyMethod()
               .AllowAnyHeader();
    });
});

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

app.UseHttpsRedirection()
   .UseCors("AppsettingsPolicy")
   .UseAuthorization()
   .UseWebSockets()
   .UseDefaultFiles()
   .UseStaticFiles();
app.MapControllers();

logger.Info("Приложение запущено.");

app.Run();
