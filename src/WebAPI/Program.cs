using Application.Common.Abstractions;
using Infrastructure;
using Infrastructure.Data;
using Serilog;
using WebAPI.HostedServices;
using WebAPI.Middleware;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://quangnn.somee.com", "https://quangnn.somee.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
        });
});


builder.Configuration
    .AddJsonFile($"QN.Expenditure.Credentials/appsettings.json");
//.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApiDocument();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddHttpClient();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddHostedService<SyncSpotOrdersService>();


var app = builder.Build();

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

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.UseExceptionMiddleware();

app.MapControllers();

app.Run();

