
using African_Nations_league.Data;
using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using MiNET;
using System.Numerics;
using System.Threading.Tasks;

namespace African_Nations_league.Controllers
{
    public class AdminController : Controller
    {
        private readonly DbSeeder _seeder;
        private readonly MongoDbService _mongo;
        private readonly UserService _userService;
        private readonly Random _rnd = new Random();
        private readonly NotificationService _notificationService;

        public AdminController(DbSeeder seeder, MongoDbService mongo, UserService userService, NotificationService notificationService)
        {
            _seeder = seeder;
            _mongo = mongo;
            _userService = userService;
            _notificationService = notificationService;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimulateMatch(string fixtureId)
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            if (string.IsNullOrEmpty(fixtureId))
                return NotFound("Fixture ID not provided");

            var fixture = await _mongo.GetFixtureByIdAsync(fixtureId);
            if (fixture == null) return NotFound("Fixture not found");

            // reset events / scores
            fixture.Events = fixture.Events ?? new List<GoalEvent>();
            fixture.Events.Clear();
            fixture.ScoreA = 0;
            fixture.ScoreB = 0;
            fixture.Status = "Playing";
            

            var random = new Random();

            // Récupérer les équipes (peuvent venir de Teams ou Users merged)
            var teamA = await _mongo.GetTeamByIdOrCodeAsync(fixture.TeamAId);
            var teamB = await _mongo.GetTeamByIdOrCodeAsync(fixture.TeamBId);

            if (teamA == null || teamB == null)
                return NotFound("One of the teams not found");

            // Safeguard: ensure players lists exist
            teamA.Players = teamA.Players ?? new List<Players>();
            teamB.Players = teamB.Players ?? new List<Players>();

            // Probabilité de marquer basée sur le rating
            double totalRating = (teamA.TeamRating + teamB.TeamRating);
            double probA = totalRating > 0 ? teamA.TeamRating / totalRating : 0.5;
            double probB = totalRating > 0 ? teamB.TeamRating / totalRating : 0.5;

            // Simulation des 90 minutes
            for (int minute = 1; minute <= 90; minute++)
            {
                if (teamA.Players.Count > 0 && random.NextDouble() < probA * 0.02)
                {
                    var scorer = teamA.Players[random.Next(teamA.Players.Count)];
                    fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamA.TeamName });
                    fixture.ScoreA++;
                }

                if (teamB.Players.Count > 0 && random.NextDouble() < probB * 0.02)
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
                    if (teamA.Players.Count > 0 && random.NextDouble() < probA * 0.01)
                    {
                        var scorer = teamA.Players[random.Next(teamA.Players.Count)];
                        fixture.Events.Add(new GoalEvent { PlayerName = scorer.PlayerName, Minute = minute, TeamName = teamA.TeamName });
                        fixture.ScoreA++;
                    }

                    if (teamB.Players.Count > 0 && random.NextDouble() < probB * 0.01)
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
            

            // Mettre à jour la fixture dans la DB
            await _mongo.UpdateFixtureAsync(fixture);

            // NOTIFICATION : inform users whose team is TeamA or TeamB
            try
            {
                // await pour que l'email parte (si tu veux non-bloquant, retire await et log)
                await _notificationService.NotifyUsersAboutMatchResult(fixture);
            }
            catch (Exception ex)
            {
                // ne fais pas planter la route si l'envoi échoue
                // si tu as un logger : _logger.LogError(ex, "NotifyUsers failed for fixture {FixtureId}", fixtureId);
            }

            TempData["Success"] = $"Match {teamA.TeamName} vs {teamB.TeamName} simulated successfully!";

            return RedirectToAction("GenerateFixtures");
        }
        // Controller (AdminController or équivalent)
        public async Task<IActionResult> GenerateFixtures()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            // 1. Récupérer teams + users (merge propre, distinct par Id)
            var teamsFromCollection = await _mongo.GetAllTeamsAsync();
            var users = await _mongo.GetAllUsersAsync();

            var teamsFromUsers = new List<Teams>();
            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.TeamId))
                {
                    teamsFromUsers.Add(new Teams
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

            // Fusion et déduplication
            var allTeams = teamsFromCollection
                .Concat(teamsFromUsers)
                .Where(t => !string.IsNullOrEmpty(t?.Id))
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // 2. toutes les fixtures existantes
            var fixtures = await _mongo.GetAllFixturesAsync();

            // 3. Supprimer fixtures qui pointent vers des teams non-existantes
            //    MAIS ne pas supprimer celles qui pointent vers des placeholders WAITING_*
            var existingTeamIds = allTeams.Select(t => t.Id).ToHashSet();
            var toDelete = fixtures.Where(f =>
                ((!string.IsNullOrEmpty(f.TeamAId) && !existingTeamIds.Contains(f.TeamAId) && !IsWaitingId(f.TeamAId)) ||
                 (!string.IsNullOrEmpty(f.TeamBId) && !existingTeamIds.Contains(f.TeamBId) && !IsWaitingId(f.TeamBId)))
            ).ToList();

            foreach (var d in toDelete) await _mongo.DeleteFixtureByIdAsync(d.Id);

            // reload fixtures après suppression
            fixtures = await _mongo.GetAllFixturesAsync();

            // --- Backfill flags for existing fixtures (safe, idempotent) ---
            // map teams by id (key: Id) so we can quickly populate flags
            var teamLookup = allTeams
                .Where(t => !string.IsNullOrEmpty(t.Id))
                .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

            var fixturesToUpdate = new List<Fixture>();
            foreach (var f in fixtures)
            {
                bool changed = false;

                if (string.IsNullOrEmpty(f.TeamAFlag) && !string.IsNullOrEmpty(f.TeamAId) && teamLookup.TryGetValue(f.TeamAId, out var ta))
                {
                    f.TeamAFlag = ta.FlagUrl; // Teams.FlagUrl (may be null)
                    changed = true;
                }

                if (string.IsNullOrEmpty(f.TeamBFlag) && !string.IsNullOrEmpty(f.TeamBId) && teamLookup.TryGetValue(f.TeamBId, out var tb))
                {
                    f.TeamBFlag = tb.FlagUrl;
                    changed = true;
                }

                if (changed) fixturesToUpdate.Add(f);
            }

            if (fixturesToUpdate.Count > 0)
            {
                // update one-by-one to be conservative / idempotent
                foreach (var uf in fixturesToUpdate)
                {
                    // Use your repository update — replace with your actual method.
                    // If you have UpdateFixtureAsync implemented, this will work.
                    // Otherwise use ReplaceOneAsync or UpdateOneAsync in your _mongo implementation.
                    await _mongo.UpdateFixtureAsync(uf);
                }

                // reload fixtures after updates
                fixtures = await _mongo.GetAllFixturesAsync();
            }
            // --- end backfill ---

            // 4. Ensure quarter finals exist (exactement 4 fixtures -> 8 slots)
            var quarterPhase = "Quarts de finale";
            var quarterFixtures = fixtures.Where(f => string.Equals(f.Phase, quarterPhase, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!quarterFixtures.Any())
            {
                // shuffle real teams
                var rnd = new Random();
                var shuffled = allTeams.OrderBy(_ => rnd.Next()).ToList();

                // build bracket of length 8 with placeholders where il manque des équipes
                var bracket8 = new List<Teams>();
                for (int i = 0; i < 8; i++)
                {
                    if (i < shuffled.Count)
                    {
                        bracket8.Add(shuffled[i]);
                    }
                    else
                    {
                        bracket8.Add(new Teams
                        {
                            Id = $"WAITING_{i + 1}",
                            TeamId = $"WAITING_{i + 1}",
                            TeamName = "Waiting for team",
                            FlagUrl = null,
                            TeamRating = 0,
                            Players = new List<Players>()
                        });
                    }
                }

                var newQuarts = new List<Fixture>();
                for (int i = 0; i + 1 < bracket8.Count; i += 2)
                {
                    var a = bracket8[i];
                    var b = bracket8[i + 1];

                    newQuarts.Add(new Fixture
                    {
                        TeamAId = a.Id,
                        TeamAName = a.TeamName,
                        TeamAFlag = a.FlagUrl, // <-- set flag
                        TeamBId = b.Id,
                        TeamBName = b.TeamName,
                        TeamBFlag = b.FlagUrl, // <-- set flag
                        Phase = quarterPhase,
                        Status = (IsWaitingId(a.Id) || IsWaitingId(b.Id)) ? "Waiting" : "Scheduled",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (newQuarts.Count > 0) await _mongo.InsertManyFixturesAsync(newQuarts);
            }
            else
            {
                // si des placeholders existent dans les quarts mais maintenant on a des équipes réelles,
                // on veut remplacer automatiquement les placeholders par des vraies équipes disponibles (optionnel).
                // Ici on va recréer les quarts si il y a maintenant >=8 vraies teams (pour remplacer placeholders)
                var quarterHasPlaceholder = quarterFixtures.Any(f => IsWaitingId(f.TeamAId) || IsWaitingId(f.TeamBId));
                if (allTeams.Count >= 8 && quarterHasPlaceholder)
                {
                    // supprimer les quarts existants et recréer à partir des 8 premières vraies équipes
                    foreach (var q in quarterFixtures) await _mongo.DeleteFixtureByIdAsync(q.Id);

                    var rnd = new Random();
                    var selected = allTeams.OrderBy(_ => rnd.Next()).Take(8).ToList();
                    var newQuarts = new List<Fixture>();
                    for (int i = 0; i + 1 < selected.Count; i += 2)
                    {
                        var A = selected[i];
                        var B = selected[i + 1];

                        newQuarts.Add(new Fixture
                        {
                            TeamAId = A.Id,
                            TeamAName = A.TeamName,
                            TeamAFlag = A.FlagUrl, // <-- set flag
                            TeamBId = B.Id,
                            TeamBName = B.TeamName,
                            TeamBFlag = B.FlagUrl, // <-- set flag
                            Phase = quarterPhase,
                            Status = "Scheduled",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    if (newQuarts.Count > 0) await _mongo.InsertManyFixturesAsync(newQuarts);
                }
            }

            // reload fixtures
            fixtures = await _mongo.GetAllFixturesAsync();

            // 5. Générer Demi-finales automatiquement si tous les quarts sont finis ET complets (no placeholders)
            await EnsureNextPhaseAsync("Quarts de finale", "Demi-finales", fixtures, allTeams);

            // reload fixtures
            fixtures = await _mongo.GetAllFixturesAsync();

            // 6. Générer Finale si demi-finales sont finis
            await EnsureNextPhaseAsync("Demi-finales", "Finale", fixtures, allTeams);

            // reload fixtures
            fixtures = await _mongo.GetAllFixturesAsync();

            // 7. ViewBag.CanSimulate : only true when the 4 quarter fixtures exist and contain NO placeholders (i.e. 8 real teams)
            quarterFixtures = fixtures.Where(f => string.Equals(f.Phase, quarterPhase, StringComparison.OrdinalIgnoreCase)).ToList();
            bool quartersExist = quarterFixtures.Count == 4;
            bool quartersHaveNoPlaceholders = quartersExist && quarterFixtures.All(f => !IsWaitingId(f.TeamAId) && !IsWaitingId(f.TeamBId));
            ViewBag.CanSimulate = quartersHaveNoPlaceholders && allTeams.Count >= 8 && !HasIncompleteFixtures(fixtures);

            // 8. return ordered fixtures
            var phaseOrder = new[] { "Quarts de finale", "Demi-finales", "Finale" };
            var ordered = fixtures.OrderBy(f =>
            {
                if (string.IsNullOrEmpty(f.Phase)) return phaseOrder.Length;
                var idx = Array.IndexOf(phaseOrder, f.Phase);
                return idx >= 0 ? idx : phaseOrder.Length;
            }).ThenBy(f => f.CreatedAt).ToList();

            return View(ordered);
        }
        // helper to detect placeholders
        private bool IsWaitingId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith("WAITING_", StringComparison.OrdinalIgnoreCase);
        }

        // helper : build next-phase fixtures from winners of previousPhase
        private async Task EnsureNextPhaseAsync(string previousPhase, string nextPhase, List<Fixture> allFixtures, List<Teams> teams)
        {
            var prevPhaseFixtures = allFixtures.Where(f => string.Equals(f.Phase, previousPhase, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!prevPhaseFixtures.Any()) return;

            // do not proceed if any fixture contains a placeholder
            if (prevPhaseFixtures.Any(f => IsWaitingId(f.TeamAId) || IsWaitingId(f.TeamBId))) return;

            // ensure all previous fixtures are complete (no missing team ids)
            if (prevPhaseFixtures.Any(f => string.IsNullOrEmpty(f.TeamAId) || string.IsNullOrEmpty(f.TeamBId))) return;

            // winners only if all previous phase fixtures are finished
            var prevFinished = prevPhaseFixtures.Where(f => string.Equals(f.Status, "Finished", StringComparison.OrdinalIgnoreCase)).ToList();
            if (prevFinished.Count != prevPhaseFixtures.Count) return;

            // compute winners
            var winners = new List<(string TeamId, string TeamName)>();
            foreach (var f in prevFinished.OrderBy(f => f.CreatedAt))
            {
                string winnerId = null, winnerName = null;
                if (f.ScoreA > f.ScoreB) { winnerId = f.TeamAId; winnerName = f.TeamAName; }
                else if (f.ScoreB > f.ScoreA) { winnerId = f.TeamBId; winnerName = f.TeamBName; }
                else
                {
                    // deterministic fallback
                    if (f.CreatedAt.Ticks % 2 == 0) { winnerId = f.TeamAId; winnerName = f.TeamAName; }
                    else { winnerId = f.TeamBId; winnerName = f.TeamBName; }
                }

                if (!string.IsNullOrEmpty(winnerId))
                    winners.Add((winnerId, winnerName));
            }

            var expectedCount = winners.Count / 2;
            if (expectedCount == 0) return;

            var existingNext = allFixtures.Where(f => string.Equals(f.Phase, nextPhase, StringComparison.OrdinalIgnoreCase)).ToList();

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

            // delete old next phase fixtures
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

        // petits helpers déjà utilisés dans la vue
        private bool HasIncompleteFixtures(IEnumerable<Fixture> fixtures)
        {
            // treat placeholder waiting ids as incomplete
            return fixtures.Any(f => string.IsNullOrEmpty(f.TeamAId) || string.IsNullOrEmpty(f.TeamBId) || IsWaitingId(f.TeamAId) || IsWaitingId(f.TeamBId));
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

        // using directives éventuels déjà présents en haut du fichier
        // using African_Nations_league.Models;
        // using System.Globalization;

        public async Task<IActionResult> Bracket()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            // 1) Récupérer fixtures, teams et users
            var fixtures = await _mongo.GetAllFixturesAsync();
            var teams = await _mongo.GetAllTeamsAsync();
            var users = await _mongo.GetAllUsersAsync();

            // 2) Construire une liste unifiée de "records" qui ont les champs dont on a besoin
            //    (Id, TeamId, TeamName, FlagUrl) — venant soit de teams soit de users.
            var unified = new List<(string Id, string TeamId, string TeamName, string FlagUrl)>();

            // From teams collection
            foreach (var t in teams)
            {
                string flag = null;
                // essaye les noms de propriété possibles présents sur Teams
                try { flag = t.GetType().GetProperty("FlagUrl")?.GetValue(t)?.ToString(); } catch { }
                if (string.IsNullOrEmpty(flag))
                {
                    try { flag = t.GetType().GetProperty("TeamFlag")?.GetValue(t)?.ToString(); } catch { }
                }
                if (string.IsNullOrEmpty(flag))
                {
                    try { flag = t.GetType().GetProperty("Flag")?.GetValue(t)?.ToString(); } catch { }
                }

                unified.Add((Id: t.Id, TeamId: t.TeamId, TeamName: t.TeamName, FlagUrl: flag));
            }

            // From users collection (many of your users store a team they manage — include them)
            foreach (var u in users)
            {
                // Some users may not have TeamName/TeamFlag — guard
                var teamName = u.TeamName;
                if (string.IsNullOrEmpty(teamName) && !string.IsNullOrEmpty(u.TeamId))
                {
                    // fallback: maybe we only have TeamId
                    teamName = u.TeamId;
                }

                string flag = null;
                try { flag = u.GetType().GetProperty("TeamFlag")?.GetValue(u)?.ToString(); } catch { }
                if (string.IsNullOrEmpty(flag))
                {
                    try { flag = u.GetType().GetProperty("FlagUrl")?.GetValue(u)?.ToString(); } catch { }
                }
                // add only if we have a team identity (TeamId or TeamName)
                if (!string.IsNullOrEmpty(u.TeamId) || !string.IsNullOrEmpty(teamName))
                {
                    unified.Add((Id: u.Id, TeamId: u.TeamId, TeamName: teamName, FlagUrl: flag));
                }
            }

            // 3) Build fast lookup dictionaries (case-insensitive for names)
            var byId = unified.Where(x => !string.IsNullOrEmpty(x.Id)).ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
            var byTeamId = unified.Where(x => !string.IsNullOrEmpty(x.TeamId)).ToDictionary(x => x.TeamId, x => x, StringComparer.OrdinalIgnoreCase);
            var byName = unified.Where(x => !string.IsNullOrEmpty(x.TeamName)).ToDictionary(x => x.TeamName.ToLowerInvariant(), x => x);

            // helper local functions
            (string Id, string TeamId, string TeamName, string FlagUrl)? FindUnifiedRecord(string key)
            {
                if (string.IsNullOrEmpty(key)) return null;

                if (byId.TryGetValue(key, out var r1)) return r1;
                if (byTeamId.TryGetValue(key, out var r2)) return r2;
                var low = key.ToLowerInvariant();
                if (byName.TryGetValue(low, out var r3)) return r3;

                // try partial match on team name
                var partial = unified.FirstOrDefault(u => !string.IsNullOrEmpty(u.TeamName) && u.TeamName.ToLowerInvariant().Contains(low));
                if (!partial.Equals(default((string, string, string, string)))) return partial;

                return null;
            }

            string PickFlag((string Id, string TeamId, string TeamName, string FlagUrl)? rec)
            {
                if (rec == null) return null;
                return rec.Value.FlagUrl; // peut être null — caller gérera le fallback visuel
            }

            // 4) Construire les viewmodels de matches (remplace les BuildMatch précédents)
            BracketMatchViewModel BuildMatch(Fixture f)
            {
                var recA = FindUnifiedRecord(f.TeamAId) ?? FindUnifiedRecord(f.TeamAName);
                var recB = FindUnifiedRecord(f.TeamBId) ?? FindUnifiedRecord(f.TeamBName);

                var teamAName = !string.IsNullOrEmpty(f.TeamAName) ? f.TeamAName : recA?.TeamName ?? (f.TeamAId ?? "Team A Waiting");
                var teamBName = !string.IsNullOrEmpty(f.TeamBName) ? f.TeamBName : recB?.TeamName ?? (f.TeamBId ?? "Team B Waiting");

                return new BracketMatchViewModel
                {
                    FixtureId = f.Id,
                    Phase = f.Phase,
                    TeamAId = f.TeamAId,
                    TeamAName = teamAName,
                    TeamAFlag = PickFlag(recA),
                    ScoreA = f.ScoreA,
                    TeamBId = f.TeamBId,
                    TeamBName = teamBName,
                    TeamBFlag = PickFlag(recB),
                    ScoreB = f.ScoreB,
                    Status = f.Status,
                    Events = f.Events ?? new List<GoalEvent>()
                };
            }

            // 5) Sélectionner et ordonner pour le bracket (comme avant)
            var quarterList = fixtures
                .Where(f => string.IsNullOrEmpty(f.Phase) || string.Equals(f.Phase, "Quarts de finale", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.CreatedAt)
                .Select(f => BuildMatch(f))
                .ToList();

            var semiList = fixtures
                .Where(f => string.Equals(f.Phase, "Demi-finales", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.CreatedAt)
                .Select(f => BuildMatch(f))
                .ToList();

            var final = fixtures.FirstOrDefault(f => string.Equals(f.Phase, "Finale", StringComparison.OrdinalIgnoreCase));
            BracketMatchViewModel finalVm = final != null ? BuildMatch(final) : null;

            var vm = new BracketViewModel
            {
                QuarterMatches = quarterList,
                SemiMatches = semiList,
                FinalMatch = finalVm
            };

            if (vm.FinalMatch != null && (vm.FinalMatch.Status == "Finished" || vm.FinalMatch.Status == "Played"))
            {
                vm.Champion = vm.FinalMatch.ScoreA > vm.FinalMatch.ScoreB ? vm.FinalMatch.TeamAName : vm.FinalMatch.TeamBName;
            }

            // Optionnel: si tu veux debuguer quelles équipes n'ont pas de Flag
            // var missing = unified.Where(u => string.IsNullOrEmpty(u.FlagUrl)).Select(u => u.TeamName).ToList();
            // TempData["MissingFlags"] = string.Join(", ", missing);

            return View("Bracket", vm);
        }
        public async Task<IActionResult> TopScorers()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied");

            var leaderboard = await _mongo.GetLeaderboardAsync();
            return View("Leaderboard", leaderboard); // ta vue TopScorers.cshtml attend List<LeaderboardEntry>
        }
    }
}
   

