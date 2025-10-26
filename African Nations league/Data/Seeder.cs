using African_Nations_league.Models;
using African_Nations_league.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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
            // 1) liste des équipes à seed (TeamName, TeamCode, FlagUrl, SportMonks TeamId)
            var teamInfos = new List<(string Name, string Code, string FlagUrl, int TeamId)>
            {
                ("Senegal", "SEN", "https://cdn.sportmonks.com/images/soccer/teams/21/21.png", 1234),
                ("Ghana",   "GHA", "https://cdn.sportmonks.com/images/soccer/teams/22/22.png", 5678),
                ("Kwa",     "KWA", "https://cdn.sportmonks.com/images/soccer/teams/23/23.png", 9101),
                ("Egypt",   "EGY", "https://cdn.sportmonks.com/images/soccer/teams/18/18546.png", 18546),
                ("Libya",   "LBY", "https://cdn.sportmonks.com/images/soccer/teams/17/18545.png", 18545),
                ("Guinea",  "GIN", "https://cdn.sportmonks.com/images/soccer/teams/20/18548.png", 18548),
                ("Togo",    "TGO", "https://cdn.sportmonks.com/images/soccer/teams/21/18549.png", 18549),
                ("Liberia", "LBR", "https://cdn.sportmonks.com/images/soccer/teams/0/11392.png", 11392)
            };

            // 2) récupérer ce qui est déjà en base
            var existingTeams = await _mongoDbService.GetAllTeamsAsync();
            var existingCount = existingTeams?.Count ?? 0;

            // Si déjà 7 ou plus -> rien faire
            if (existingCount >= 7)
            {
                return;
            }

            // noms déjà présents pour éviter doublons
            var existingNames = new HashSet<string>(existingTeams.Select(t => t.TeamName), StringComparer.OrdinalIgnoreCase);

            foreach (var info in teamInfos)
            {
                // stop si on a atteint 7 équipes
                existingCount = (await _mongoDbService.GetAllTeamsAsync()).Count; // safe check (optional)
                if (existingCount >= 7) break;

                if (existingNames.Contains(info.Name))
                {
                    // déjà présent -> skip
                    continue;
                }

                try
                {
                    // Récupérer les joueurs via l'API SportMonks (ton service)
                    var players = await _sportMonksService.GetPlayersByTeamIdAsync(info.TeamId) ?? new List<Players>();

                    // Calculer la moyenne des ratings des joueurs (sur 100 si tu as transformé)
                    double teamRating = 0;
                    if (players.Count > 0)
                    {
                        teamRating = players.Average(p => (double)p.Rating);
                    }

                    // Préparer l'objet Teams
                    var team = new Teams
                    {
                        // Laisser Id null — MongoDB générera l'ObjectId automatiquement
                        TeamName = info.Name,
                        TeamCode = info.Code,
                        FlagUrl = info.FlagUrl,
                        TeamRating = teamRating,
                        Players = players
                    };

                    // Insérer dans MongoDB via ton service
                    await _mongoDbService.InsertTeamAsync(team);

                    // Mettre à jour la liste locale pour éviter ré-inserts dans la même boucle
                    existingNames.Add(info.Name);
                    existingCount++;
                }
                catch (Exception ex)
                {
                    // Log minimal (remplace par ton logger si tu en as)
                    Console.WriteLine($"Erreur lors du seed de {info.Name} : {ex.Message}");
                }
            }
        }
    }
}
