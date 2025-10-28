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
            var teams = await _mongo.GetAllTeamsAsync();
            return View(teams);
        }

        // Détail d'une équipe
        public async Task<IActionResult> TeamDetails(string id)
        {
            if (!IsAdmin()) return RedirectToAction(nameof(AccessDenied));
            var team = await _mongo.GetTeamByIdAsync(id);
            if (team == null) return NotFound();
            return View(team);
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
            var home = _mongo.GetTeamByIdAsync(homeTeamId).GetAwaiter().GetResult();
            var away = _mongo.GetTeamByIdAsync(awayTeamId).GetAwaiter().GetResult();

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
