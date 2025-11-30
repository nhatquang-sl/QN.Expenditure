using System.Reflection;
using Auth.Infrastructure;
using Auth.Infrastructure.Data;
using Cex.Infrastructure;
using Lib.Application.Abstractions;
using Lib.Notifications;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using ServiceDefaults;
using WebAPI.HostedServices;
using WebAPI.Middleware;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

//builder.AddRedisOutputCache("redis-cache");
builder.Configuration
    .AddJsonFile("QN.Expenditure.Credentials/appsettings.json")
    .AddJsonFile($"QN.Expenditure.Credentials/appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddEnvironmentVariables();
builder.AddServiceDefaults();
builder.AddRedisOutputCache("Redis");
builder.AddRedisDistributedCache("Redis");
builder.Services.AddHybridCache();
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration =
//         builder.Configuration.GetConnectionString("Redis");
// });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var corsOrigins = (builder.Configuration.GetValue<string>("CorsOrigins") ?? "").Split(",")
            .Select(x => x.Trim()).ToArray();
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);


builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
);
// Add services to the container.
// builder.Services.AddTransient(_ =>
//     new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger());
builder.Services.AddControllers();
builder.Services.AddTelegramNotifier(builder.Configuration);
builder.Services.AddAuthInfrastructureServices(builder.Configuration);
builder.Services.AddCexInfrastructureServices(builder.Configuration);

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// builder.Services.AddHostedService<SpotGridService>();
// builder.Services.AddHostedService<RunIndicatorService>();
// builder.Services.AddHostedService<SyncTradeHistoryService>();
// builder.Services.AddHostedService<ListenCexWebsocketService>();

var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

builder.Services.AddOpenApiDocument(options =>
{
    // Add JWT bearer token security scheme
    options.AddSecurity("JWT", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Type into the textbox: Bearer {your JWT token}"
    });

    // Apply the security scheme to all operations
    options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWT"));

    // Post process the document information
    options.PostProcess = document =>
    {
        document.Info = new OpenApiInfo
        {
            Version = version,
            Title = builder.Environment.EnvironmentName
            // Description = "An ASP.NET Core Web API for managing ToDo items",
            // TermsOfService = "https://example.com/terms",
            // Contact = new OpenApiContact
            // {
            //     Name = "Example Contact",
            //     Url = "https://example.com/contact"
            // },
            // License = new OpenApiLicense
            // {
            //     Name = "Example License",
            //     Url = "https://example.com/license"
            // }
        };
    };
});

var app = builder.Build();
app.Logger.LogInformation("OTEL_EXPORTER_OTLP_ENDPOINT {endpoint}",
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

app.Logger.LogInformation("API VERSION: {version}", version);
//app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    await app.InitializeDatabaseAsync();
}

// Add OpenAPI 3.0 document serving middleware
// Available at: http://localhost:<port>/swagger/v1/swagger.json
app.UseOpenApi();

// Add web UIs to interact with the document
// Available at: http://localhost:<port>/swagger
app.UseSwaggerUi();

app.UseOutputCache();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.UseExceptionMiddleware();

app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();