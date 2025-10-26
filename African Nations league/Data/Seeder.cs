using African_Nations_league.Models;
using African_Nations_league.Services;
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
            // Liste des équipes avec leurs TeamId pour SportMonks
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

            foreach (var teamInfo in teamInfos)
            {
                // Récupère les joueurs via le service SportMonks
                var players = await _sportMonksService.GetPlayersByTeamIdAsync(teamInfo.TeamId);

                // Met à jour TeamName et TeamFlag pour chaque joueur
                foreach (var player in players)
                {
                    player.TeamName = teamInfo.Name;
                    player.TeamFlag = teamInfo.FlagUrl;
                }

                // Crée l'objet Teams avec la liste de joueurs mise à jour
                var team = new Teams
                {
                    TeamName = teamInfo.Name,
                    TeamCode = teamInfo.Code,
                    FlagUrl = teamInfo.FlagUrl,
                    Players = players,
                    TeamRating = players.Count > 0 ? (int)players.Average(p => p.Rating) : 0
                };

                // Insère dans MongoDB
                await _mongoDbService.InsertTeamAsync(team);
            }
        }
    }
}
