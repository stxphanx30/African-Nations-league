using African_Nations_league.Data;
using African_Nations_league.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ----------------------- Configuration & secrets -----------------------
// Exemple : builder.Configuration["MONGO_URI"] ou variables d'environnement
// Vérifie que MONGO_URI et SPORTMONKS_API_KEY sont définis
var mongoUri = builder.Configuration["MONGO_URI"];
if (string.IsNullOrWhiteSpace(mongoUri))
{
    throw new InvalidOperationException("MONGO_URI is not configured. Set it in appsettings or environment variables.");
}

// ----------------------- MVC + Session -----------------------
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MatchService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ----------------------- Mongo client & services -----------------------
// IMongoClient : singleton construit à partir de la config
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var conn = cfg["MONGO_URI"] ?? throw new InvalidOperationException("MONGO_URI not set");
    return new MongoClient(conn);
});

// Services qui dépendent de IMongoClient / IConfiguration
builder.Services.AddSingleton<UserService>();      // ctor(IMongoClient)
builder.Services.AddSingleton<MongoDbService>();   // si ctor prend IConfiguration ou IMongoClient, assure-toi qu'il existe
// SportMonksService : HttpClient injection + IConfiguration (API key)
builder.Services.AddHttpClient<SportMonksService>();

// Seeder (scoped because it peut appeler des services scoped)
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddTransient<IEmailService, SendGridEmailService>();
builder.Services.AddTransient<NotificationService>();

// ----------------------- Build app -----------------------
var app = builder.Build();

// ----------------------- Dev-only seeding (safe) -----------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var seeder = services.GetRequiredService<DbSeeder>();
        var userService = services.GetRequiredService<UserService>();

        // Seed teams (async)
        await seeder.SeedTeamsAsync();

        // Ensure admin exists (method implemented in your DbSeeder)
        // If your seeder exposes EnsureAdminUserAsync, otherwise adapt/remove this call
        await seeder.EnsureAdminUserAsync(userService);

        logger.LogInformation("Seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur pendant le seed (development).");
    }
}

// ----------------------- Middleware pipeline -----------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

// ----------------------- Routes -----------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Loading}/{action=Landing}/{id?}");

app.Run();
