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
        public async Task<IActionResult> SimulateMatch(string fixtureId)
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            if (string.IsNullOrEmpty(fixtureId))
                return NotFound("Fixture ID not provided");

            var fixture = await _mongo.GetFixtureByIdAsync(fixtureId);


            fixture.Events.Clear(); // reset events
            fixture.ScoreA = 0;
            fixture.ScoreB = 0;
            fixture.Status = "Playing";

            var random = new Random();

            // Récupérer les équipes
            var teamA = await _mongo.GetTeamByIdOrCodeAsync(fixture.TeamAId);
            var teamB = await _mongo.GetTeamByIdOrCodeAsync(fixture.TeamBId);

            if (teamA == null || teamB == null)
                return NotFound("One of the teams not found");

            // Probabilité de marquer basée sur le rating
            double probA = teamA.TeamRating / (teamA.TeamRating + teamB.TeamRating);
            double probB = teamB.TeamRating / (teamA.TeamRating + teamB.TeamRating);

            // Simulation des 90 minutes
            for (int minute = 1; minute <= 90; minute++)
            {
                if (random.NextDouble() < probA * 0.02)
                {
                    var scorer = teamA.Players[random.Next(teamA.Players.Count)];
                    fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamA.TeamName });
                    fixture.ScoreA++;
                }

                if (random.NextDouble() < probB * 0.02)
                {
                    var scorer = teamB.Players[random.Next(teamB.Players.Count)];
                    fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamB.TeamName });
                    fixture.ScoreB++;
                }
            }

            // Prolongations si égalité
            if (fixture.ScoreA == fixture.ScoreB)
            {
                for (int minute = 91; minute <= 120; minute++)
                {
                    if (random.NextDouble() < probA * 0.01)
                    {
                        var scorer = teamA.Players[random.Next(teamA.Players.Count)];
                        fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamA.TeamName });
                        fixture.ScoreA++;
                    }

                    if (random.NextDouble() < probB * 0.01)
                    {
                        var scorer = teamB.Players[random.Next(teamB.Players.Count)];
                        fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamB.TeamName });
                        fixture.ScoreB++;
                    }
                }
            }

            // Pénaltys si toujours égalité
            if (fixture.ScoreA == fixture.ScoreB)
            {
                int penA = 0, penB = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (random.NextDouble() < 0.75) penA++;
                    if (random.NextDouble() < 0.75) penB++;
                }

                while (penA == penB)
                {
                    if (random.NextDouble() < 0.75) penA++;
                    if (random.NextDouble() < 0.75) penB++;
                }

                fixture.Events.Add(new GoalEvent
                {
                    PlayerName = "Penalties",
                    Minute = 120,
                    TeamName = $"{teamA.TeamName} {penA} - {penB} {teamB.TeamName}"
                });

                fixture.ScoreA += penA;
                fixture.ScoreB += penB;
            }

            fixture.Status = "Finished";

            // ✅ Mettre à jour la fixture dans la DB
            await _mongo.UpdateFixtureAsync(fixture);

            TempData["Success"] = $"Match {teamA.TeamName} vs {teamB.TeamName} simulated successfully!";

            return RedirectToAction("GenerateFixtures");
        }

        public async Task<IActionResult> GenerateFixtures()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            // 1. Récupérer teams + users (merge)
            var teams = await _mongo.GetAllTeamsAsync();
            var users = await _mongo.GetAllUsersAsync();
            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.TeamId) && !teams.Any(t => t.TeamId == u.TeamId))
                {
                    teams.Add(new Teams
                    {
                        Id = u.TeamId,
                        TeamId = u.TeamId,
                        TeamName = u.TeamName,
                        FlagUrl = u.TeamFlag,
                        TeamRating = u.TeamRating,
                        Players = u.Squad
                    });
                }
            }

            // 2. toutes les fixtures existantes
            var fixtures = await _mongo.GetAllFixturesAsync();

            // 3. Supprimer fixtures qui pointent vers des teams non-existantes
            var existingTeamIds = teams.Select(t => t.Id).ToHashSet();
            var toDelete = fixtures.Where(f =>
                (!string.IsNullOrEmpty(f.TeamAId) && !existingTeamIds.Contains(f.TeamAId)) ||
                (!string.IsNullOrEmpty(f.TeamBId) && !existingTeamIds.Contains(f.TeamBId))
            ).ToList();

            foreach (var d in toDelete) await _mongo.DeleteFixtureByIdAsync(d.Id);

            // reload fixtures après suppression
            fixtures = await _mongo.GetAllFixturesAsync();

            // 4. Ensure quarter finals exist (pairs). If none present, create quarts from scratch
            var quarterPhase = "Quarts de finale";
            var quarterFixtures = fixtures.Where(f => string.IsNullOrEmpty(f.Phase) || string.Equals(f.Phase, quarterPhase, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!quarterFixtures.Any())
            {
                var rnd = new Random();
                var shuffled = teams.OrderBy(_ => rnd.Next()).ToList();
                var newQuarts = new List<Fixture>();
                for (int i = 0; i + 1 < shuffled.Count; i += 2)
                {
                    newQuarts.Add(new Fixture
                    {
                        TeamAId = shuffled[i].Id,
                        TeamAName = shuffled[i].TeamName,
                        TeamBId = shuffled[i + 1].Id,
                        TeamBName = shuffled[i + 1].TeamName,
                        Phase = quarterPhase,
                        Status = "Scheduled"
                    });
                }
                if (newQuarts.Count > 0) await _mongo.InsertManyFixturesAsync(newQuarts);
            }

            // reload fixtures
            fixtures = await _mongo.GetAllFixturesAsync();

            // 5. Générer Demi-finales automatiquement si tous les quarts sont finis
            await EnsureNextPhaseAsync("Quarts de finale", "Demi-finales", fixtures, teams);

            // reload fixtures (demis maybe created)
            fixtures = await _mongo.GetAllFixturesAsync();

            // 6. Générer Finale si demi-finales sont finis
            await EnsureNextPhaseAsync("Demi-finales", "Finale", fixtures, teams);

            // reload fixtures (final maybe created)
            fixtures = await _mongo.GetAllFixturesAsync();

            // 7. ViewBag to enable simulate only when we have at least 8 teams (UX)
            ViewBag.CanSimulate = teams.Count >= 8;

            // 8. return ordered fixtures (order by mentioned phase order)
            var phaseOrder = new[] { "Quarts de finale", "Demi-finales", "Finale" };
            var ordered = fixtures.OrderBy(f =>
            {
                if (string.IsNullOrEmpty(f.Phase)) return 0;
                var idx = Array.IndexOf(phaseOrder, f.Phase);
                return idx >= 0 ? idx : phaseOrder.Length;
            }).ThenBy(f => f.CreatedAt).ToList();

            return View(ordered);
        }

        // helper : build next-phase fixtures from winners of previousPhase
        private async Task EnsureNextPhaseAsync(string previousPhase, string nextPhase, List<Fixture> allFixtures, List<Teams> teams)
        {
            // winners from previousPhase (only finished)
            var prevFinished = allFixtures.Where(f => string.Equals(f.Phase, previousPhase, StringComparison.OrdinalIgnoreCase) && f.Status == "Finished").ToList();

            // if no finished matches in previous phase -> nothing to do
            if (prevFinished.Count == 0) return;

            // compute winners list in the order of prevFinished by CreatedAt (or other stable ordering)
            var winners = new List<(string TeamId, string TeamName)>();
            foreach (var f in prevFinished.OrderBy(f => f.CreatedAt))
            {
                string winnerId = null, winnerName = null;
                if (f.ScoreA > f.ScoreB) { winnerId = f.TeamAId; winnerName = f.TeamAName; }
                else if (f.ScoreB > f.ScoreA) { winnerId = f.TeamBId; winnerName = f.TeamBName; }
                else
                {
                    // fallback tie-breaker: try event penalties, else random deterministic
                    if (f.Events != null && f.Events.Any(e => e.PlayerName?.ToLower().Contains("penal") == true))
                    {
                        // crude: if an event mentions penalties, pick team with penalty goals recorded in events text (not ideal)
                        // fallback random:
                        var rnd = new Random();
                        if (rnd.Next(0, 2) == 0) { winnerId = f.TeamAId; winnerName = f.TeamAName; }
                        else { winnerId = f.TeamBId; winnerName = f.TeamBName; }
                    }
                    else
                    {
                        var rnd = new Random();
                        if (rnd.Next(0, 2) == 0) { winnerId = f.TeamAId; winnerName = f.TeamAName; }
                        else { winnerId = f.TeamBId; winnerName = f.TeamBName; }
                    }
                }

                if (!string.IsNullOrEmpty(winnerId))
                    winners.Add((winnerId, winnerName));
            }

            // expected number of fixtures in nextPhase = winners.Count / 2
            var expectedCount = winners.Count / 2;
            if (expectedCount == 0) return;

            // existing nextPhase fixtures
            var existingNext = allFixtures.Where(f => string.Equals(f.Phase, nextPhase, StringComparison.OrdinalIgnoreCase)).ToList();

            // if existingNext count matches expectedCount AND team-sets match -> nothing to do
            bool needRecreate = false;
            if (existingNext.Count != expectedCount) needRecreate = true;
            else
            {
                var expectedPairs = new HashSet<string>();
                for (int i = 0; i + 1 < winners.Count; i += 2)
                    expectedPairs.Add(winners[i].TeamId + "|" + winners[i + 1].TeamId);

                var existingPairs = new HashSet<string>(existingNext.Select(f => (f.TeamAId ?? "") + "|" + (f.TeamBId ?? "")));
                if (!expectedPairs.SetEquals(existingPairs)) needRecreate = true;
            }

            if (!needRecreate) return;

            // delete old nextPhase fixtures
            foreach (var nf in existingNext) await _mongo.DeleteFixtureByIdAsync(nf.Id);

            // create new nextPhase fixtures from winners sequentially
            var newFixtures = new List<Fixture>();
            for (int i = 0; i + 1 < winners.Count; i += 2)
            {
                newFixtures.Add(new Fixture
                {
                    TeamAId = winners[i].TeamId,
                    TeamAName = winners[i].TeamName,
                    TeamBId = winners[i + 1].TeamId,
                    TeamBName = winners[i + 1].TeamName,
                    Phase = nextPhase,
                    Status = "Scheduled",
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (newFixtures.Count > 0) await _mongo.InsertManyFixturesAsync(newFixtures);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetFixtures()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            try
            {
                await _mongo.DeleteAllFixturesAsync();
                TempData["Success"] = "All fixtures have been deleted. Tournament reset.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error while resetting fixtures: " + ex.Message;
            }

            return RedirectToAction(nameof(GenerateFixtures));
        }
    }
}