using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
namespace African_Nations_league.Controllers
{
  
        public class TeamsController : Controller
        {
            private readonly SportMonksService _sportMonksService;
            private readonly MongoDbService _mongoDbService;

            public TeamsController(SportMonksService sportMonksService, MongoDbService mongoDbService)
            {
                _sportMonksService = sportMonksService;
                _mongoDbService = mongoDbService;
            }
        public async Task<IActionResult> Index()
        {
            
            var teams = await _mongoDbService.GetAllTeamsAsync();
            return View(teams);
        }
        public async Task<IActionResult> SeedTeams()
            {
                // ID des équipes
                var teamsInfo = new (int id, string name, string code, string flagUrl)[]
                {
                (18547, "Sénégal", "SEN", "https://cdn.sportmonks.com/images/soccer/teams/19/18547.png"),
                (18550, "Ghana", "GHA", "https://cdn.sportmonks.com/images/soccer/teams/22/18550.png"),
                (18551, "Kwa", "CIV", "https://cdn.sportmonks.com/images/soccer/teams/23/18551.png")
                };

                foreach (var (id, name, code, flagUrl) in teamsInfo)
                {
                    var players = await _sportMonksService.GetPlayersByTeamIdAsync(id);
                    await _mongoDbService.InsertTeamAsync(new Models.Teams
                    {
                        TeamName = name,
                        TeamCode = code,
                        FlagUrl = flagUrl,
                        Players = players
                    });
                }

                return Content("Équipes insérées avec succès !");
            }
        }
    }


