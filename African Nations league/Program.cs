using African_Nations_league.Data;
using African_Nations_league.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 🔹 MongoDbService : on doit passer la connection string et le nom de la DB
builder.Services.AddSingleton<MongoDbService>();

// 🔹 SportMonksService : HttpClient sera injecté automatiquement
builder.Services.AddHttpClient<SportMonksService>();

// 🔹 DbSeeder pour le peuplement initial
builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

// 🔹 Peupler la DB au démarrage
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedTeamsAsync(); // Insère les 8 équipes si elles ne sont pas déjà présentes
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Loading}/{action=Landing}/{id?}");

app.Run();
