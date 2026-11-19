using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradeDesk.Api;
using TradeDesk.Core.Data;
using TradeDesk.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TradeExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITradeService, TradeService>();

// SQLite by default so the demo runs anywhere; set Database:Provider=SqlServer to use SQL Server.
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connection = builder.Configuration.GetConnectionString("TradeDesk")
    ?? throw new InvalidOperationException("Connection string 'TradeDesk' is missing.");
builder.Services.AddDbContext<TradeDbContext>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(connection);
    else o.UseSqlite(connection);
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
    db.Database.EnsureCreated();
    SeedData.Ensure(db, DateOnly.FromDateTime(DateTime.Today));
}

app.UseExceptionHandler();
app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

app.Run();

public partial class Program;
