using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace African_Nations_league.Data
{
    public class DbSeeder
    {
        private readonly MongoDbService _mongoDbService;
        private readonly SportMonksService _sportMonksService;
        private readonly ILogger<DbSeeder> _logger;

        public DbSeeder(MongoDbService mongoDbService, SportMonksService sportMonksService, ILogger<DbSeeder> logger)
        {
            _mongoDbService = mongoDbService;
            _sportMonksService = sportMonksService;
            _logger = logger;
        }

        // Retourne le nombre d'équipes insérées / upsertées pendant cet appel
        public async Task<int> SeedTeamsAsync()
        {
            // si déjà >=7 équipes on ne seed pas
            var existingCount = await _mongoDbService.CountTeamsAsync();
            _logger.LogInformation("DbSeeder: équipes existantes = {count}", existingCount);
            if (existingCount >= 7)
            {
                _logger.LogInformation("DbSeeder: skip seed (déjà >= 7 équipes).");
                return 0;
            }

            var teamInfos = new List<(string Name, string Code, string FlagUrl, int TeamId)>
{
    ("South Africa", "RSA", "https://cdn.sportmonks.com/images/soccer/teams/27/18555.png", 18555),
    ("Senegal", "SEN", "https://cdn.sportmonks.com/images/soccer/teams/30/18558.png", 18558),
    ("Congo DR", "COD", "https://cdn.sportmonks.com/images/soccer/teams/24/18552.png", 18552),
    ("Egypt", "EGY", "https://cdn.sportmonks.com/images/soccer/teams/18/18546.png", 18546),
    ("Libya", "LBY", "https://cdn.sportmonks.com/images/soccer/teams/17/18545.png", 18545),
    ("Guinea", "GIN", "https://cdn.sportmonks.com/images/soccer/teams/20/18548.png", 18548),
    ("Ghana", "GHA", "https://cdn.sportmonks.com/images/soccer/teams/25/18553.png", 18553)


};

            int seeded = 0;

            foreach (var ti in teamInfos)
            {
                // stop si on a atteint 7 après inserts précédents
                existingCount = await _mongoDbService.CountTeamsAsync();
                if (existingCount >= 7) break;

                List<Players> players = null;
                try
                {
                    players = await _sportMonksService.GetPlayersByTeamIdAsync(ti.TeamId);
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning(ex, "DbSeeder: erreur en récupérant players pour teamId {teamId}", ti.TeamId);
                    players = new List<Players>();
                }

                // set team meta for players
                foreach (var p in players)
                {
                    p.TeamName = ti.Name;
                    p.TeamFlag = ti.FlagUrl;

                }

                double teamRating = 0;
                if (players != null && players.Count > 0)
                    teamRating = players.Average(p => (double)p.Rating);

                var teamDoc = new Teams
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TeamName = ti.Name,
                    TeamCode = ti.Code,
                    FlagUrl = ti.FlagUrl,
                    TeamRating = teamRating,
                    Players = players ?? new List<Players>()
                };

                try
                {
                    // Upsert (replace if TeamName exists)
                    await _mongoDbService.UpsertTeamAsync(teamDoc);
                    seeded++;
                    _logger.LogInformation("DbSeeder: upserted team {name}", ti.Name);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "DbSeeder: erreur pendant l'upsert pour {name}", ti.Name);
                }
            }

            _logger.LogInformation("DbSeeder: terminé. équipes seedées: {count}", seeded);
            return seeded;
        }

        public async Task EnsureAdminUserAsync(UserService userService)
        {
            var adminEmail = "admin@africannations.local";
            var existing = await userService.GetByEmailAsync(adminEmail);
            if (existing == null)
            {
                var admin = new User
                {
                    FullName = "Tournament Admin",
                    Email = adminEmail,
                    Role = "Admin",
                    PhoneNumber = "",
                    PasswordHash = ComputeHash("Admin@123")
                };
                await userService.CreateUserAsync(admin);
                _logger.LogInformation("DbSeeder: admin créé ({email})", adminEmail);
            }
            else
            {
                _logger.LogInformation("DbSeeder: admin déjà présent ({email})", adminEmail);
            }
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            return System.Convert.ToBase64String(bytes);
        }
    }
}
