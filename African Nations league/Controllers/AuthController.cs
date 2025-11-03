using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace African_Nations_league.Controllers
{
    public class AuthController : Controller
    {
        private readonly MongoDbService _mongoDbService;
        private readonly UserService _userService;
        private readonly SportMonksService _sportMonksService;
       
        public AuthController(UserService userService, SportMonksService sportMonksService, MongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
            _userService = userService;
            _sportMonksService = sportMonksService;
            
        }

        // GET: /Auth/Login
        [HttpGet]
        public IActionResult Login() => View();

        // POST: /Auth/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email et mot de passe requis");
                return View();
            }

            var user = await _userService.GetByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email ou mot de passe incorrect");
                return View();
            }

            // Vérifie le hash
            if (user.PasswordHash != ComputeHash(password))
            {
                ModelState.AddModelError("", "Email ou mot de passe incorrect");
                return View();
            }

            // Si c'est un représentant, récupérer la squad si elle n'est pas déjà chargée
            if (user.Role == "Representative" && (user.Squad == null || user.Squad.Count == 0) && !string.IsNullOrEmpty(user.TeamId))
            {
                try
                {
                    var squad = await _sportMonksService.GetPlayersByTeamIdAsync(int.Parse(user.TeamId));
                    user.Squad = squad.Count > 23 ? squad.GetRange(0, 23) : squad;

                    if (user.Squad.Count > 0)
                    {
                        user.TeamName = user.Squad[0].TeamName;
                        user.TeamFlag = user.Squad[0].TeamFlag;
                    }

                    // Optionnel : save updated user back to DB (if you want to persist squad)
                    // await _userService.UpdateUserAsync(user);
                }
                catch (Exception ex)
                {
                    // log si besoin, mais on continue login même si API fail
                }
            }

            // Stocker l'utilisateur dans la session (ou via cookie selon besoin)
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role);

            // Redirection selon le rôle
            if (user.Role == "Admin")
                return RedirectToAction("Index", "Admin");
            else // Representative
                return RedirectToAction("Index", "Representative");
        }
        // GET: /Auth/Signup
        // GET: /Auth/Signup
        public IActionResult Signup()
        {
            // Option A: static list quick for the form (value = SportMonks id or Mongo id)
            var teams = new List<SelectListItem>
    {
        new SelectListItem("Senegal", "18558"),
        new SelectListItem("South Africa", "18555"),
        new SelectListItem("Congo DR", "18552"),
        new SelectListItem("Egypt", "18546"),
        new SelectListItem("Togo", "18549")
    };

            ViewBag.TeamOptions = teams;
            return View();
        }

        // POST: /Auth/Signup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(string FullName, string Email, string PhoneNumber, string TeamId, string ManagerName, string password)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(FullName))
            {
                ModelState.AddModelError("", "Les champs obligatoires ne sont pas remplis");
                // repopulate select
                ViewBag.TeamOptions = new List<SelectListItem>
        {
            new SelectListItem("Senegal", "18558"),
            new SelectListItem("South Africa", "18555"),
            new SelectListItem("Congo DR", "18552"),
            new SelectListItem("Egypt", "18546"),
            new SelectListItem("Togo", "18549")
        };
                return View();
            }

            var existingUser = await _userService.GetByEmailAsync(Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email déjà utilisé");
                ViewBag.TeamOptions = new List<SelectListItem>
        {
            new SelectListItem("Senegal", "18558"),
            new SelectListItem("South Africa", "18555"),
            new SelectListItem("Congo DR", "18552"),
            new SelectListItem("Egypt", "18546"),
            new SelectListItem("Togo", "18549")
        };
                return View();
            }

            var user = new User
            {
                FullName = FullName,
                Email = Email,
                PhoneNumber = PhoneNumber,
                Role = "Representative",
                ManagerName = ManagerName
            };

            user.PasswordHash = ComputeHash(password);

            // --- static lookup to map SportMonks id -> name + flag (fallback) ---
            var teamLookup = new Dictionary<string, (string Name, string Flag)>
    {
        { "18558", ("Senegal", "https://cdn.sportmonks.com/images/soccer/teams/??/18558.png") }, // adjust if you have exact path
        { "18555", ("South Africa", "https://cdn.sportmonks.com/images/soccer/teams/27/18555.png") },
        { "18552", ("Congo DR", "https://cdn.sportmonks.com/images/soccer/teams/24/18552.png") },
        { "18546", ("Egypt", "https://cdn.sportmonks.com/images/soccer/teams/18/18546.png") },
        { "18549", ("Togo", "https://cdn.sportmonks.com/images/soccer/teams/21/18549.png") }
    };

            Teams foundTeam = null;

            // Try case 1: TeamId is a Mongo ObjectId -> find in Teams collection
            if (!string.IsNullOrEmpty(TeamId) && MongoDB.Bson.ObjectId.TryParse(TeamId, out _))
            {
                foundTeam = await _mongoDbService.GetTeamByIdOrCodeAsync(TeamId); // tolerant lookup
                if (foundTeam != null)
                {
                    user.TeamId = foundTeam.Id;        // store Teams document id
                    user.TeamName = foundTeam.TeamName;
                    user.TeamFlag = foundTeam.FlagUrl;
                    user.TeamRating = foundTeam.TeamRating;
                    user.Squad = foundTeam.Players ?? new List<Players>();
                }
            }

            // Case 2: TeamId looks like SportMonks numeric id (e.g. "18555")
            if (foundTeam == null && !string.IsNullOrEmpty(TeamId))
            {
                // If we have static lookup, fill name/flag
                if (teamLookup.TryGetValue(TeamId, out var meta))
                {
                    user.TeamName = meta.Name;
                    user.TeamFlag = meta.Flag;
                    user.TeamId = TeamId; // store SportMonks id so we can later use it
                }

                // Try to fetch squad from SportMonks to populate players & compute rating
                if (int.TryParse(TeamId, out var smTeamId))
                {
                    try
                    {
                        var squad = await _sportMonksService.GetPlayersByTeamIdAsync(smTeamId);
                        user.Squad = squad.Count > 23 ? squad.GetRange(0, 23) : squad;
                        if (user.Squad.Count > 0)
                        {
                            user.TeamName = user.TeamName ?? user.Squad[0].TeamName;
                            user.TeamFlag = user.TeamFlag ?? user.Squad[0].TeamFlag;
                            user.TeamRating = user.Squad.Average(p => p.Rating);
                        }
                    }
                    catch
                    {
                        // ignore API issues for signup; user still created with best-effort metadata
                    }
                }
            }

            // final safety: if TeamName still empty but ManagerName or selection present, set something
            user.TeamName = user.TeamName ?? "Unknown Team";

            await _userService.CreateUserAsync(user);
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Vide la session
            HttpContext.Session.Clear();

            // Redirige vers login
            return RedirectToAction("Login", "Auth");
        }
        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password ?? ""));
            return System.Convert.ToBase64String(bytes);
        }
    }
}
