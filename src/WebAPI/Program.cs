using Application.Common.Abstractions;
using Infrastructure;
using Infrastructure.Data;
using Serilog;
using WebAPI.Middleware;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile($"QN.Expenditure.Credentials/appsettings.json");

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddHttpClient();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    await app.InitializeDatabaseAsync();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseExceptionMiddleware();

app.MapControllers();

app.Run();

