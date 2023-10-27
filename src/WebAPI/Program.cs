using Infrastructure;
using Infrastructure.Data;
using WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile($"QN.Expenditure.Credentials/appsettings.json");
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

