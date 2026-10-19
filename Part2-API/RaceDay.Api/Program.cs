using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RaceDayDb")
    ?? throw new InvalidOperationException("Connection string 'RaceDayDb' is missing from appsettings.json.");

builder.Services.AddDbContext<RaceDayDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Send and accept enums as text ("Run") instead of numbers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Server-side session. The cookie only holds a session id; user id and role stay on the server.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "Event management API for South African road running, walking and cycling events. " +
                      "Log in through POST /api/auth/login first. The session cookie is then sent automatically from this page."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Create or update the database when the API starts.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

    if (db.Database.IsRelational())
    {
        if (db.Database.GetMigrations().Any())
        {
            db.Database.Migrate();
        }
        else
        {
            // No migration has been added yet. Build the schema straight from the model.
            db.Database.EnsureCreated();
        }

        DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    }
    else
    {
        // In-memory database used by the unit tests.
        db.Database.EnsureCreated();
    }
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
});

app.UseSession();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Lets the test project start the API in memory.
public partial class Program
{
}
