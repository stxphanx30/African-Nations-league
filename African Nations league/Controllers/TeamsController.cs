using African_Nations_league.Models;
using African_Nations_league.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
namespace African_Nations_league.Controllers
{
  
        public class TeamsController : Controller
        {
            private readonly SportMonksService _sportMonksService;
            private readonly MongoDbService _mongoDbService;
        private readonly MongoDbService _mongo;
        public TeamsController(SportMonksService sportMonksService, MongoDbService mongoDbService, MongoDbService mongo)
            {
                _sportMonksService = sportMonksService;
                _mongoDbService = mongoDbService;
            _mongo = mongo;
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
        public async Task<IActionResult> Teams()
        {
           
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
        public async Task<IActionResult> Leaderboard()
        {

            var leaderboard = await _mongo.GetLeaderboardAsync();
            return View("Leaderboard", leaderboard); // ta vue TopScorers.cshtml attend List<LeaderboardEntry>
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
    }


