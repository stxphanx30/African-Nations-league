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

        // Dashboard: profile + team overview only
        public async Task<IActionResult> Index()
        {
            if (!IsRepresentative())
                return RedirectToAction("AccessDenied", "Admin");

            // 1️⃣ Récupérer l'utilisateur connecté via son email
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "Auth");

            var user = await _userService.GetCurrentUserAsync(email);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            Teams team = null;

            // 2️⃣ Chercher l'équipe dans la collection Teams si TeamId est valide
            if (!string.IsNullOrEmpty(user.TeamId))
            {
                team = await _mongo.GetTeamByIdOrCodeAsync(user.TeamId);
            }

            // 3️⃣ Si l'équipe n'existe pas dans Teams, prendre les infos de l'utilisateur (Users collection)
            if (team == null && !string.IsNullOrEmpty(user.TeamId))
            {
                team = new Teams
                {
                    Id = user.TeamId,
                    TeamName = user.TeamName,
                    FlagUrl = user.TeamFlag,
                    TeamRating = user.TeamRating,
                    Players = user.Squad
                };
            }

            // 4️⃣ Préparer le viewmodel
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
    }

    // small viewmodel for profile page
    public class RepresentativeProfileViewModel
    {
        public User User { get; set; }
        public Teams Team { get; set; }
    }
}