using African_Nations_league.Data;
using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace African_Nations_league.Controllers
{
    public class AdminController : Controller
    {
        private readonly DbSeeder _seeder;
        private readonly MongoDbService _mongo;
        private readonly UserService _userService;
        private readonly Random _rnd = new Random();
        public AdminController(DbSeeder seeder, MongoDbService mongo, UserService userService)
        {
            _seeder = seeder;
            _mongo = mongo;
            _userService = userService;
        }

        // Vérifie si current user est admin via session
        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(role) && role == "Admin";
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));

            var teams = await _mongo.GetAllTeamsAsync();
            var users = await _userService.GetAllAsync(); // j'assume que tu as GetAllAsync
            var vm = new AdminDashboardViewModel
            {
                TeamsCount = teams?.Count ?? 0,
                UsersCount = users?.Count ?? 0,
                Teams = teams,
                Users = users
            };

            return View(vm);
        }

        // Liste des équipes
        public async Task<IActionResult> Teams()
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));

            // 1️⃣ Récupérer toutes les équipes de la collection Teams
            var mongoTeams = await _mongo.GetAllTeamsAsync();

            var mongoTeamViewModels = mongoTeams.Select(t => new TeamViewModel
            {
                Id = t.Id,                    // Mongo ObjectId string
                TeamId = t.TeamId,            // SportMonks id si tu le stockes dans Teams.TeamId
                TeamName = t.TeamName,
                TeamFlag = t.FlagUrl,
                ManagerName = null,           // pas de manager dans Teams collection
                TeamRating = t.TeamRating,
                Squad = t.Players
            }).ToList();

            // 2️⃣ Récupérer toutes les équipes "attachées" aux users
            var users = await _mongo.GetAllUsersAsync();
            var userTeamViewModels = new List<TeamViewModel>();

            foreach (var u in users)
            {
                if (string.IsNullOrEmpty(u.TeamId) && string.IsNullOrEmpty(u.TeamName)) continue;

                // On crée un TeamViewModel à partir des données stockées dans le user
                var tv = new TeamViewModel
                {
                    Id = u.Id,               // l'id du user (unique pour cette entrée)
                    TeamId = u.TeamId,       // peut être sportmonks id ou mongo id selon comment tu l'as stocké
                    TeamName = u.TeamName,
                    TeamFlag = u.TeamFlag,
                    ManagerName = u.ManagerName,
                    TeamRating = u.TeamRating,
                    Squad = u.Squad
                };

                userTeamViewModels.Add(tv);
            }

            // 3️⃣ Fusionner: on veut la liste complète sans doublons (préserver les teams de la collection Teams)
            // clé de déduplication : TeamId si présent, sinon TeamName
            var merged = new List<TeamViewModel>(mongoTeamViewModels);

            foreach (var uTv in userTeamViewModels)
            {
                bool exists = false;

                if (!string.IsNullOrEmpty(uTv.TeamId))
                    exists = merged.Any(m => !string.IsNullOrEmpty(m.TeamId) && m.TeamId == uTv.TeamId);

                if (!exists && !string.IsNullOrEmpty(uTv.TeamName))
                    exists = merged.Any(m => !string.IsNullOrEmpty(m.TeamName) && m.TeamName.Equals(uTv.TeamName, System.StringComparison.OrdinalIgnoreCase));

                if (!exists)
                    merged.Add(uTv);
            }

            // Optionnel: trier par TeamName
            merged = merged.OrderBy(t => t.TeamName).ToList();

            return View(merged); // la vue Admin/Teams doit accepter List<TeamViewModel>
        }

        // Détail d'une équipe (Admin)
        public async Task<IActionResult> TeamDetails(string id)
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));
            if (string.IsNullOrEmpty(id)) return NotFound("Team id not provided");

            // Construire une liste unifiée d'équipes à partir des deux collections
            var teamList = new List<TeamDetailsViewModel>();

            // 1) Teams collection
            var mongoTeams = await _mongo.GetAllTeamsAsync();
            foreach (var t in mongoTeams)
            {
                teamList.Add(new TeamDetailsViewModel
                {
                    Id = t.Id,
                    TeamId = t.TeamId,
                    TeamName = t.TeamName,
                    TeamFlag = t.FlagUrl,
                    ManagerName = null,
                    TeamRating = t.TeamRating,
                    Squad = t.Players
                });
            }

            // 2) Users collection -> équipe définie par user
            var users = await _mongo.GetAllUsersAsync();
            foreach (var u in users)
            {
                // éviter doublons selon TeamId ou TeamName
                var already = teamList.Any(x =>
                    (!string.IsNullOrEmpty(x.TeamId) && !string.IsNullOrEmpty(u.TeamId) && x.TeamId == u.TeamId) ||
                    (!string.IsNullOrEmpty(x.TeamName) && !string.IsNullOrEmpty(u.TeamName) && x.TeamName.Equals(u.TeamName, System.StringComparison.OrdinalIgnoreCase))
                );

                if (!already)
                {
                    teamList.Add(new TeamDetailsViewModel
                    {
                        Id = u.TeamId ?? u.Id,    // si TeamId est sportmonks id, on le met, sinon on garde user.Id
                        TeamId = u.TeamId,
                        TeamName = u.TeamName,
                        TeamFlag = u.TeamFlag,
                        ManagerName = u.ManagerName,
                        TeamRating = u.TeamRating,
                        Squad = u.Squad
                    });
                }
            }

            // 3) Rechercher la team demandée — tolérant : on cherche par Id (mongo id), TeamId (sportmonks id) ou TeamName
            TeamDetailsViewModel vm = null;

            // by Id (mongo teams.Id or user.TeamId stored as mongo id)
            vm = teamList.FirstOrDefault(t => !string.IsNullOrEmpty(t.Id) && t.Id == id);

            // by TeamId (sportmonks numeric id stored in TeamId)
            if (vm == null)
                vm = teamList.FirstOrDefault(t => !string.IsNullOrEmpty(t.TeamId) && t.TeamId == id);

            // by TeamName (friendly fallback)
            if (vm == null)
                vm = teamList.FirstOrDefault(t => !string.IsNullOrEmpty(t.TeamName) && t.TeamName.Equals(id, System.StringComparison.OrdinalIgnoreCase));

            if (vm == null)
                return NotFound("Team not found");

            return View(vm); // Admin/TeamDetails.cshtml doit accepter TeamDetailsViewModel
        }

        // Liste des utilisateurs
        public async Task<IActionResult> Users()
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));
            var users = await _userService.GetAllAsync();
            return View(users);
        }

        // Seed manuel (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Seed()
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));
            await _seeder.SeedTeamsAsync();
            TempData["SeedMessage"] = "Seed executed.";
            return RedirectToAction(nameof(Index));
        }
        // GET: Admin/Matches
        // GET: /Admin/Matches
        public async Task<IActionResult> Matches()
        {
            var teams = await _mongo.GetAllTeamsAsync();

            // shuffle
            var shuffled = teams.OrderBy(t => _rnd.Next()).ToList();

            var pairs = new List<MatchPairViewModel>();
            for (int i = 0; i < shuffled.Count; i += 2)
            {
                var home = shuffled[i];
                Teams away = (i + 1 < shuffled.Count) ? shuffled[i + 1] : null;

                pairs.Add(new MatchPairViewModel
                {
                    HomeTeamId = home.Id,
                    HomeTeamName = home.TeamName,
                    HomeTeamRating = home.TeamRating,
                    AwayTeamId = away?.Id,
                    AwayTeamName = away?.TeamName ?? "—",
                    AwayTeamRating = away?.TeamRating ?? 0
                });
            }

            return View(pairs);
        }

        // POST: /Admin/SimulateMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SimulateMatch([FromForm] string homeTeamId, [FromForm] string awayTeamId)
        {
            // if awayTeamId is null -> nothing to simulate
            if (string.IsNullOrEmpty(awayTeamId))
                return BadRequest(new { error = "No opponent" });

            // fetch teams from DB synchronously is okay here since we already have ids;
            // better to use async version if fetching: but to keep simple return simulation only
            // Ideally fetch rating from DB:
            var home = _mongo.GetTeamByIdOrCodeAsync(homeTeamId).GetAwaiter().GetResult();
            var away = _mongo.GetTeamByIdOrCodeAsync(awayTeamId).GetAwaiter().GetResult();

            if (home == null || away == null)
                return NotFound();

            // simulate
            var result = SimulateOneMatch(home, away);

            // Optionally: persist match result to DB (you can add a Matches collection) - omitted for now

            return Ok(result); // returns JSON
        }

        private SimulatedMatch SimulateOneMatch(Teams home, Teams away)
        {
            // Probability based on TeamRating
            double homeR = Math.Max(1, home.TeamRating);
            double awayR = Math.Max(1, away.TeamRating);
            double total = homeR + awayR;
            double homeWinProb = homeR / total;
            double awayWinProb = awayR / total;

            // base goals 0..3 random
            int homeGoals = _rnd.Next(0, 4);
            int awayGoals = _rnd.Next(0, 4);

            // nudge scores by probability
            if (homeWinProb > awayWinProb) homeGoals = Math.Max(homeGoals, _rnd.Next(1, 3));
            else awayGoals = Math.Max(awayGoals, _rnd.Next(1, 3));

            // Simulate scorers (simple names using jersey number or index)
            var scorers = new List<string>();
            for (int i = 0; i < homeGoals; i++)
                scorers.Add($"{home.TeamName} - player #{_rnd.Next(1, 24)} (min {_rnd.Next(1, 90)})");
            for (int i = 0; i < awayGoals; i++)
                scorers.Add($"{away.TeamName} - player #{_rnd.Next(1, 24)} (min {_rnd.Next(1, 90)})");

            string resultText = "Simulé";
            if (homeGoals > awayGoals) resultText = $"{home.TeamName} gagne";
            else if (awayGoals > homeGoals) resultText = $"{away.TeamName} gagne";
            else resultText = "Nul";

            return new SimulatedMatch
            {
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                HomeTeam = home.TeamName,
                AwayTeam = away.TeamName,
                HomeScore = homeGoals,
                AwayScore = awayGoals,
                Result = resultText,
                GoalScorers = scorers
            };
        }
    }
}
