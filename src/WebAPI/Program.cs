using Auth.Infrastructure;
using Auth.Infrastructure.Data;
using Cex.Infrastructure;
using Lib.Application.Abstractions;
using Lib.Notifications;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using WebAPI.HostedServices;
using WebAPI.Middleware;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddRedisOutputCache("redis-cache");
builder.Services.AddStackExchangeRedisCache(options =>
options.Configuration = builder.Configuration.GetConnectionString("RedisCache"));
builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("http://localhost:5174")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Configuration
    .AddJsonFile("QN.Expenditure.Credentials/appsettings.json")
    .AddJsonFile($"QN.Expenditure.Credentials/appsettings.{builder.Environment.EnvironmentName}.json", true, true);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
);
// Add services to the container.
builder.Services.AddTransient(_ =>
    new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger());
builder.Services.AddControllers();
builder.Services.AddTelegramNotifier(builder.Configuration);
builder.Services.AddAuthInfrastructureServices(builder.Configuration);
builder.Services.AddCexInfrastructureServices(builder.Configuration);

builder.Services.AddHttpClient();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// builder.Services.AddHostedService<SpotGridService>();
builder.Services.AddHostedService<RunIndicatorService>();
// builder.Services.AddHostedService<ListenCexWebsocketService>();

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
            Version = "v1",
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

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    var connectionString = app.Configuration.GetConnectionString("PostgresConnection");
    app.Logger.LogInformation("Postgres connection string: {ConnectionString}", connectionString);

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

app.MapControllers();

app.Run();