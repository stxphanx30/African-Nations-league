using African_Nations_league.Models;
using African_Nations_league.Services;
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

        public DbSeeder(MongoDbService mongoDbService, SportMonksService sportMonksService)
        {
            _mongoDbService = mongoDbService;
            _sportMonksService = sportMonksService;
        }

        public async Task SeedTeamsAsync()
        {
            // si déjà >=7 équipes on ne seed pas
            var existingCount = await _mongoDbService.CountTeamsAsync();
            if (existingCount >= 7) return;

            var teamInfos = new List<(string Name, string Code, string FlagUrl, int TeamId)>
            {
                ("Senegal", "SEN", "https://cdn.sportmonks.com/images/soccer/teams/21/21.png", 1234),
                ("Ghana", "GHA", "https://cdn.sportmonks.com/images/soccer/teams/22/22.png", 5678),
                ("Kwa", "KWA", "https://cdn.sportmonks.com/images/soccer/teams/23/23.png", 9101),
                ("Egypt", "EGY", "https://cdn.sportmonks.com/images/soccer/teams/18/18546.png", 18546),
                ("Libya", "LBY", "https://cdn.sportmonks.com/images/soccer/teams/17/18545.png", 18545),
                ("Guinea", "GIN", "https://cdn.sportmonks.com/images/soccer/teams/20/18548.png", 18548),
                ("Togo", "TGO", "https://cdn.sportmonks.com/images/soccer/teams/21/18549.png", 18549),
                ("Liberia", "LBR", "https://cdn.sportmonks.com/images/soccer/teams/0/11392.png", 11392)
            };

            foreach (var ti in teamInfos)
            {
                // stop si on a atteint 7 après inserts précedents
                existingCount = await _mongoDbService.CountTeamsAsync();
                if (existingCount >= 7) break;

                // récupère les joueurs (GetPlayersByTeamIdAsync retourne Players list avec Rating)
                var players = await _sportMonksService.GetPlayersByTeamIdAsync(ti.TeamId);

                // assure que TeamName/TeamFlag sur chaque player
                foreach (var p in players)
                {
                    p.TeamName = ti.Name;
                    p.TeamFlag = ti.FlagUrl;
                }

                // calcul du team rating (moyenne des players.Rating)
                double teamRating = 0;
                if (players != null && players.Count > 0)
                {
                    teamRating = players.Average(p => (double)p.Rating);
                }

                // creer document Teams (ne pas assigner Id pour laisser Mongo créer l'_id)
                var teamDoc = new Teams
                {
                    TeamName = ti.Name,
                    TeamCode = ti.Code,
                    FlagUrl = ti.FlagUrl,
                    TeamRating = teamRating,
                    Players = players
                };

                // Upsert : si TeamName existe on remplace, sinon insert
                await _mongoDbService.UpsertTeamAsync(teamDoc);
            }
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
                    PasswordHash = ComputeHash("Admin@123") // méthode utilitaire
                };
                await userService.CreateUserAsync(admin);
            }
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
