using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System;

namespace African_Nations_league.Controllers
{
    public class AuthController : Controller
    {
        private readonly UserService _userService;
        private readonly SportMonksService _sportMonksService;

        public AuthController(UserService userService, SportMonksService sportMonksService)
        {
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
        [HttpGet]
        public IActionResult Signup() => View();

        // POST: /Auth/Signup
        // Note : signup here is intended for Representatives only (Admin prepopulated via seeder)
        [HttpPost]
        public async Task<IActionResult> Signup(string FullName, string Email, string PhoneNumber, string TeamId, string password)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(FullName))
            {
                ModelState.AddModelError("", "Les champs obligatoires ne sont pas remplis");
                return View();
            }

            var existingUser = await _userService.GetByEmailAsync(Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email déjà utilisé");
                return View();
            }

            var user = new User
            {
                FullName = FullName,
                Email = Email,
                PhoneNumber = PhoneNumber,
                Role = "Representative",
                TeamId = TeamId
            };

            user.PasswordHash = ComputeHash(password);

            // Si Representative, récupérer la squad via l'API (optionnel, on l'enregistre dans user.Squad)
            if (!string.IsNullOrEmpty(user.TeamId))
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
                }
                catch
                {
                    // ignore API errors for now (signup should still succeed)
                }
            }

            await _userService.CreateUserAsync(user);
            return RedirectToAction("Login");
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
