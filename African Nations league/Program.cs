using African_Nations_league.Data;
using African_Nations_league.Services;

var builder = WebApplication.CreateBuilder(args);

// config
builder.Services.AddControllersWithViews();

// Register services
builder.Services.AddSingleton<MongoDbService>(); // MongoDbService prend IConfiguration via ctor
builder.Services.AddHttpClient<SportMonksService>();
builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

// Optionnel : en DEV, tu peux exécuter le seed automatiquement (ou utiliser l'endpoint admin)
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedTeamsAsync();
}


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
