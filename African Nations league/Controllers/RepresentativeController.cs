using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Match = African_Nations_league.Services.Match;

namespace African_Nations_league.Controllers
{
    public class RepresentativeController : Controller
    {
        private readonly UserService _userService;
        private readonly MongoDbService _mongo;
        private readonly MatchService _matchService;

        public RepresentativeController(UserService userService, MongoDbService mongo, MatchService matchService)
        {
            _userService = userService;
            _mongo = mongo;
            _matchService = matchService;
        }

        // helper to check role
        // helper to check role
        private bool IsRepresentative()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(role) && role == "Representative";
        }
        public async Task<IActionResult> TopScorers()
        {

            var leaderboard = await _mongo.GetLeaderboardAsync();
            return View("Leaderboard", leaderboard); // ta vue TopScorers.cshtml attend List<LeaderboardEntry>
        }
        // Dashboard: profile + team overview only
        public async Task<IActionResult> Index()
        {
            if (!IsRepresentative())
                return RedirectToAction("AccessDenied", "Admin");

            // 1) Récupérer l'email de session (fallback possible aux claims si tu veux)
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "Auth");

            // 2) Charger l'utilisateur (depuis user service)
            var user = await _userService.GetCurrentUserAsync(email);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            Teams team = null;

            // 3) Si User.TeamId est renseigné -> essayer de charger la team dans la collection Teams
            if (!string.IsNullOrEmpty(user.TeamId))
            {
                try
                {
                    // Essaie de récupérer la team depuis la collection Teams (par Id ou code)
                    team = await _mongo.GetTeamByIdOrCodeAsync(user.TeamId);
                }
                catch
                {
                    // ignore exception et fallback ci-dessous
                    team = null;
                }

                // 4) Si la team n'existe pas dans la collection Teams mais que l'user a des infos de team,
                //    construire un objet Teams à partir des infos présentes sur l'utilisateur (Users collection).
                if (team == null && (!string.IsNullOrEmpty(user.TeamName) || user.Squad != null))
                {
                    team = new Teams
                    {
                        Id = user.TeamId,
                        TeamId = user.TeamId,
                        TeamName = user.TeamName ?? "Unnamed team",
                        FlagUrl = user.TeamFlag,
                        TeamRating = user.TeamRating,
                        Players = user.Squad ?? new List<Players>() // adapte Players au type exact
                    };
                }
            }

            // 5) Si aucun TeamId n'est défini sur l'utilisateur, on n'affiche PAS d'équipe aléatoire.
            //    On peut afficher un message "No team assigned" dans la vue.
            //    (Si tu veux, tu peux ici chercher "par email" dans Users pour trouver une team liée, 
            //     mais normalement user.TeamId doit exister.)

            var vm = new RepresentativeDashboardViewModel
            {
                User = user,
                Team = team
            };

            return View(vm);
        }
        // GET: /Representative/Teams
        public async Task<IActionResult> Teams()
        {
            if (!IsRepresentative())
                return RedirectToAction("AccessDenied", "Admin");

            // 1️⃣ Récupérer le représentant courant
            var email = HttpContext.Session.GetString("UserEmail");
            var user = await _userService.GetCurrentUserAsync(email);

            TeamViewModel userTeamViewModel = null;

            // 2️⃣ Si l'utilisateur a une équipe attachée, crée un viewmodel depuis User
            if (user != null && !string.IsNullOrEmpty(user.TeamId))
            {
                userTeamViewModel = new TeamViewModel
                {
                    Id = user.Id, // ou un autre identifiant unique
                    TeamId = user.TeamId,
                    TeamName = user.TeamName,
                    TeamFlag = user.TeamFlag,
                    ManagerName = user.ManagerName,
                    TeamRating = user.TeamRating,
                    Squad = user.Squad
                };
            }

            // 3️⃣ Récupérer toutes les équipes de la collection Teams
            var mongoTeams = await _mongo.GetAllTeamsAsync();

            var mongoTeamViewModels = mongoTeams.Select(t => new TeamViewModel
            {
                Id = t.Id,
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                TeamFlag = t.FlagUrl,
                ManagerName = "N/A", // pas de manager dans Teams
                TeamRating = t.TeamRating,
                Squad = t.Players
            }).ToList();

            // 4️⃣ Ajouter l'équipe du représentant en premier si elle existe
            if (userTeamViewModel != null)
            {
                // éviter doublons si elle existe déjà dans Teams
                if (!mongoTeamViewModels.Any(t => t.TeamId == userTeamViewModel.TeamId))
                {
                    mongoTeamViewModels.Insert(0, userTeamViewModel);
                }
            }

            return View(mongoTeamViewModels); // vue attend List<TeamViewModel>
        }
        // GET: /Representative/TeamDetails/{id}
        public async Task<IActionResult> TeamDetails(string id)
        {
            if (!IsRepresentative()) return RedirectToAction("AccessDenied", "Admin");
            if (string.IsNullOrEmpty(id)) return NotFound("Team ID not provided");

            // 1️⃣ Récupérer toutes les équipes de Teams
            var mongoTeams = await _mongo.GetAllTeamsAsync();
            var teamList = mongoTeams.Select(t => new TeamDetailsViewModel
            {
                Id = t.Id,
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                TeamFlag = t.FlagUrl,
                TeamRating = t.TeamRating,
                Squad = t.Players
            }).ToList();

            // 2️⃣ Ajouter les équipes des Users
            var users = await _mongo.GetAllUsersAsync();
            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.TeamId) && !teamList.Any(t => t.TeamId == u.TeamId))
                {
                    teamList.Add(new TeamDetailsViewModel
                    {
                        Id = u.TeamId,
                        TeamId = u.TeamId,
                        TeamName = u.TeamName,
                        TeamFlag = u.TeamFlag,
                        ManagerName = u.ManagerName,
                        TeamRating = u.TeamRating,
                        Squad = u.Squad
                    });
                }
            }

            // 3️⃣ Chercher l'équipe demandée
            TeamDetailsViewModel vm = null;

            // Essayer d'abord ObjectId
            vm = teamList.FirstOrDefault(t => t.Id == id);
            // Sinon essayer TeamId
            if (vm == null)
                vm = teamList.FirstOrDefault(t => t.TeamId == id);

            if (vm == null)
                return NotFound("Team not found");

            return View(vm);
        }

        // GET: /Representative/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!IsRepresentative()) return RedirectToAction("AccessDenied", "Admin");

            var email = HttpContext.Session.GetString("UserEmail");
            var user = await _userService.GetCurrentUserAsync(email);

            Teams team = null;
            if (!string.IsNullOrEmpty(user?.TeamId))
                team = await _mongo.GetTeamByIdOrCodeAsync(user.TeamId);

            var vm = new RepresentativeProfileViewModel
            {
                User = user,
                Team = team
            };

            return View(vm);
        }

        // GET: /Representative/EditProfile
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            if (!IsRepresentative()) return RedirectToAction("AccessDenied", "Admin");

            var email = HttpContext.Session.GetString("UserEmail");
            var user = await _userService.GetCurrentUserAsync(email);
            if (user == null) return RedirectToAction("Login", "Auth");

            return View(user);
        }

        // POST: /Representative/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(User model)
        {
            if (!IsRepresentative()) return RedirectToAction("AccessDenied", "Admin");

            var email = HttpContext.Session.GetString("UserEmail");
            var user = await _userService.GetCurrentUserAsync(email);
            if (user == null) return RedirectToAction("Login", "Auth");

            // Update allowed fields only
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.ManagerName = model.ManagerName;

            // If you allow team changes here, be careful: ensure model.TeamId is a valid Mongo ID
            await _userService.UpdateUserAsync(user);

            TempData["Success"] = "Profile updated.";
            return RedirectToAction("Profile");
        }

        // POST: /Representative/DeleteProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile()
        {
            if (!IsRepresentative()) return RedirectToAction("AccessDenied", "Admin");

            var email = HttpContext.Session.GetString("UserEmail");
            var user = await _userService.GetCurrentUserAsync(email);
            if (user == null) return RedirectToAction("Login", "Auth");

            await _userService.DeleteUserAsync(user.Id);

            // clear session
            HttpContext.Session.Remove("UserEmail");
            HttpContext.Session.Remove("UserRole");

            return RedirectToAction("Login", "Auth");
        }
        public async Task<IActionResult> Bracket()
        {

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
    }

    // small viewmodel for profile page
    public class RepresentativeProfileViewModel
    {
        public User User { get; set; }
        public Teams Team { get; set; }
    }
}