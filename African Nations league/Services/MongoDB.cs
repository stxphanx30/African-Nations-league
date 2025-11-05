
using African_Nations_league.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<Teams> _teams;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Fixture> _fixturesCollection;
        public MongoDbService(IConfiguration config)
        {
            var connectionString = config["MONGO_URI"];
            var dbName = config["MONGO_DBNAME"];
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbName);
            _teams = database.GetCollection<Teams>("teams");
            _users = database.GetCollection<User>("users");
            _fixturesCollection = database.GetCollection<Fixture>("Fixtures");
        }

        public async Task InsertTeamAsync(Teams team)
        {
            await _teams.InsertOneAsync(team);
        }
        // Dans ton MongoDbService
        public async Task<Fixture> GetFixtureByIdAsync(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return null;

            var filter = Builders<Fixture>.Filter.Eq(f => f.Id, fixtureId);
            return await _fixturesCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task UpdateFixtureAsync(Fixture fixture)
        {
            if (fixture == null || string.IsNullOrEmpty(fixture.Id))
                throw new ArgumentException("Invalid fixture");

            var filter = Builders<Fixture>.Filter.Eq(f => f.Id, fixture.Id);
            await _fixturesCollection.ReplaceOneAsync(filter, fixture);
        }
        public async Task UpsertTeamAsync(Teams team)
        {
            var filter = Builders<Teams>.Filter.Eq(t => t.TeamName, team.TeamName);
            var options = new ReplaceOptions { IsUpsert = true };
            await _teams.ReplaceOneAsync(filter, team, options);
        }

        public async Task<List<Teams>> GetAllTeamsAsync()
        {
            return await _teams.Find(t => true).ToListAsync();
        }
        public async Task<List<Fixture>> GenerateFixturesAsync(List<Teams> teams)
        {
            var fixtures = new List<Fixture>();
            var rnd = new Random();

            // On mélange les équipes
            var shuffled = teams.OrderBy(x => rnd.Next()).ToList();

            // On fait les paires deux à deux
            for (int i = 0; i < shuffled.Count - 1; i += 2)
            {
                fixtures.Add(new Fixture
                {
                    TeamAId = shuffled[i].Id,
                    TeamAName = shuffled[i].TeamName,
                    TeamBId = shuffled[i + 1].Id,
                    TeamBName = shuffled[i + 1].TeamName
                });
            }

            // Stocker dans la collection "Fixtures"
            await _fixturesCollection.InsertManyAsync(fixtures);

            return fixtures;
        }

        public async Task<long> CountTeamsAsync()
        {
            return await _teams.CountDocumentsAsync(FilterDefinition<Teams>.Empty);
        }
        public async Task<List<Fixture>> GetAllFixturesAsync()
        {
            return await _fixturesCollection.Find(_ => true).ToListAsync();
        }
        public async Task DeleteFixtureByIdAsync(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            // Essayer de parser en ObjectId si nécessaire
            if (!ObjectId.TryParse(fixtureId, out var objId))
                return;

            var filter = Builders<Fixture>.Filter.Eq(f => f.Id, fixtureId);
            await _fixturesCollection.DeleteOneAsync(filter);
        }
        public async Task InsertManyFixturesAsync(List<Fixture> fixtures)
        {
            if (fixtures == null || fixtures.Count == 0)
                return;

            await _fixturesCollection.InsertManyAsync(fixtures);
        }
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }
        public async Task<List<Fixture>> UpdateFixturesAsync(List<Teams> allTeams)
        {
            // 1️⃣ Récupérer toutes les fixtures
            var fixtures = await _fixturesCollection.Find(_ => true).ToListAsync();

            // 2️⃣ Supprimer les fixtures avec des équipes qui n'existent plus
            var existingTeamIds = allTeams.Select(t => t.Id).ToList();
            foreach (var f in fixtures)
            {
                if (!existingTeamIds.Contains(f.TeamAId) || !existingTeamIds.Contains(f.TeamBId))
                {
                    await _fixturesCollection.DeleteOneAsync(x => x.Id == f.Id);
                }
            }

            // 3️⃣ Récupérer les équipes qui n'ont pas encore de fixture
            fixtures = await _fixturesCollection.Find(_ => true).ToListAsync();
            var usedTeamIds = fixtures.SelectMany(f => new[] { f.TeamAId, f.TeamBId }).Distinct().ToList();
            var missingTeams = allTeams.Where(t => !usedTeamIds.Contains(t.Id)).ToList();

            // 4️⃣ Créer des fixtures pour les équipes manquantes
            var rnd = new Random();
            var shuffled = missingTeams.OrderBy(x => rnd.Next()).ToList();
            var newFixtures = new List<Fixture>();

            for (int i = 0; i < shuffled.Count - 1; i += 2)
            {
                newFixtures.Add(new Fixture
                {
                    TeamAId = shuffled[i].Id,
                    TeamAName = shuffled[i].TeamName,
                    TeamBId = shuffled[i + 1].Id,
                    TeamBName = shuffled[i + 1].TeamName
                });
            }

            if (newFixtures.Count > 0)
                await _fixturesCollection.InsertManyAsync(newFixtures);

            return await _fixturesCollection.Find(_ => true).ToListAsync();
        }
        public async Task<Teams> GetTeamByIdOrCodeAsync(string idOrCode)
        {
            if (string.IsNullOrEmpty(idOrCode))
                return null;

            // Normalize: try ObjectId first
            ObjectId objId;
            bool isObjectId = ObjectId.TryParse(idOrCode, out objId);

            var filter = Builders<Teams>.Filter.Or(
                isObjectId ? Builders<Teams>.Filter.Eq(t => t.Id, idOrCode) : Builders<Teams>.Filter.Empty,
                Builders<Teams>.Filter.Eq(t => t.TeamId, idOrCode),
                Builders<Teams>.Filter.Eq(t => t.TeamName, idOrCode)
            );

            return await _teams.Find(filter).FirstOrDefaultAsync();
        }
        public async Task CleanFixturesAsync(List<Teams> allTeams)
        {
            // Récupérer toutes les fixtures
            var fixtures = await _fixturesCollection.Find(_ => true).ToListAsync();

            // Liste des IDs d'équipes valides
            var validTeamIds = allTeams.Select(t => t.Id).ToList();

            // Identifier les fixtures qui contiennent des équipes invalides
            var invalidFixtures = fixtures
                .Where(f => !validTeamIds.Contains(f.TeamAId) ||
                            (!string.IsNullOrEmpty(f.TeamBId) && !validTeamIds.Contains(f.TeamBId)))
                .ToList();

            // Supprimer les fixtures invalides
            if (invalidFixtures.Count > 0)
            {
                var idsToDelete = invalidFixtures.Select(f => f.Id).ToList();
                var filter = Builders<Fixture>.Filter.In(f => f.Id, idsToDelete);
                await _fixturesCollection.DeleteManyAsync(filter);
            }
        }
        public async Task<List<Fixture>> AddMissingFixturesAsync(List<Teams> allTeams)
        {
            // 1️⃣ Récupérer toutes les fixtures existantes
            var existingFixtures = await _fixturesCollection.Find(_ => true).ToListAsync();

            // 2️⃣ Identifier les équipes déjà utilisées
            var usedTeamIds = existingFixtures
                .SelectMany(f => new[] { f.TeamAId, f.TeamBId })
                .Distinct()
                .ToList();

            // 3️⃣ Filtrer les équipes manquantes
            var missingTeams = allTeams
                .Where(t => !usedTeamIds.Contains(t.Id))
                .ToList();

            if (missingTeams.Count == 0)
                return existingFixtures; // rien à ajouter

            // 4️⃣ Mélanger les équipes manquantes pour générer des paires aléatoires
            var rnd = new Random();
            var shuffled = missingTeams.OrderBy(t => rnd.Next()).ToList();

            var newFixtures = new List<Fixture>();
            for (int i = 0; i < shuffled.Count - 1; i += 2)
            {
                newFixtures.Add(new Fixture
                {
                    TeamAId = shuffled[i].Id,
                    TeamAName = shuffled[i].TeamName,
                    TeamBId = shuffled[i + 1].Id,
                    TeamBName = shuffled[i + 1].TeamName
                });
            }

            // Si nombre d'équipes impaire, la dernière reste "waiting"
            if (shuffled.Count % 2 != 0)
            {
                newFixtures.Add(new Fixture
                {
                    TeamAId = shuffled.Last().Id,
                    TeamAName = shuffled.Last().TeamName,
                    TeamBId = null,
                    TeamBName = "Waiting"
                });
            }

            // 5️⃣ Ajouter ces nouvelles fixtures à Mongo
            if (newFixtures.Count > 0)
                await _fixturesCollection.InsertManyAsync(newFixtures);

            // 6️⃣ Retourner toutes les fixtures existantes + nouvelles
            existingFixtures.AddRange(newFixtures);
            return existingFixtures;
        }
        public async Task EnsureIndexesAsync()
        {
            var indexKeys = Builders<Teams>.IndexKeys.Ascending(t => t.TeamName);
            var indexModel = new CreateIndexModel<Teams>(indexKeys, new CreateIndexOptions { Unique = true, Name = "ux_teamname" });
            await _teams.Indexes.CreateOneAsync(indexModel);
        }
        // Delete every fixture (reset tournament)
        public async Task DeleteAllFixturesAsync()
        {
            await _fixturesCollection.DeleteManyAsync(_ => true);
        }
        // si pas déjà present
// ... à l'intérieur de la classe MongoDbService ...

// ... inside MongoDbService class

public async Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        // 1) récupérer toutes les fixtures
        var fixtures = await _fixturesCollection.Find(Builders<Fixture>.Filter.Empty).ToListAsync();

        // 2) récupérer les teams (collection teams) et users (collection users)
        var teams = await _teams.Find(Builders<Teams>.Filter.Empty).ToListAsync();
        var users = await _users.Find(Builders<User>.Filter.Empty).ToListAsync();

        // 3) construire un lookup teamName -> flag (merge teams + users)
        var teamFlagByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in teams)
        {
            if (!string.IsNullOrEmpty(t.TeamName) && !string.IsNullOrEmpty(t.FlagUrl))
                teamFlagByName[t.TeamName] = t.FlagUrl;
        }
        foreach (var u in users)
        {
            if (!string.IsNullOrEmpty(u.TeamName) && !string.IsNullOrEmpty(u.TeamFlag))
                teamFlagByName[u.TeamName] = u.TeamFlag;
        }

        // 4) build a player-picture lookup from teams and users squads (PlayerName -> ImagePath)
        var playerPictureByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in teams)
        {
            if (t.Players == null) continue;
            foreach (var p in t.Players)
            {
                if (string.IsNullOrEmpty(p?.PlayerName)) continue;
                if (!string.IsNullOrEmpty(p.ImagePath))
                    playerPictureByName[p.PlayerName] = p.ImagePath;
            }
        }

        foreach (var u in users)
        {
            if (u.Squad == null) continue;
            foreach (var p in u.Squad)
            {
                if (string.IsNullOrEmpty(p?.PlayerName)) continue;
                if (!string.IsNullOrEmpty(p.ImagePath))
                    playerPictureByName[p.PlayerName] = p.ImagePath;
            }
        }

        // 5) Collect all goal events across fixtures, ignore penalty summary events (e.g. PlayerName == "Penalties" or contains "penal")
        var goalEvents = new List<(string PlayerName, string TeamName)>();
        foreach (var f in fixtures)
        {
            if (f?.Events == null) continue;
            foreach (var e in f.Events)
            {
                if (e == null) continue;
                var pn = e.PlayerName?.Trim();
                var tn = e.TeamName?.Trim();
                if (string.IsNullOrEmpty(pn) || string.IsNullOrEmpty(tn)) continue;
                var low = pn.ToLowerInvariant();
                if (low.Contains("penal") || low.Contains("penalties") || low.Contains("penalty")) continue;

                // Only add if pn looks like a player (optionally: skip "Penalties", "Own Goal" etc.)
                goalEvents.Add((PlayerName: pn, TeamName: tn));
            }
        }
        var grouped = goalEvents
            .GroupBy(g => new
            {
                PlayerKey = g.PlayerName?.ToLowerInvariant() ?? string.Empty,
                TeamKey = g.TeamName?.ToLowerInvariant() ?? string.Empty
            })
            .Select(gr =>
            {
                var first = gr.First();
                var playerNameOriginal = first.PlayerName;
                var teamNameOriginal = first.TeamName;

                teamFlagByName.TryGetValue(teamNameOriginal, out var flag);
                playerPictureByName.TryGetValue(playerNameOriginal, out var pic);

                return new LeaderboardEntry
                {
                    PlayerName = playerNameOriginal,
                    TeamName = teamNameOriginal,
                    TeamFlag = flag,
                    PlayerPicture = pic,
                    Goals = gr.Count()
                };
            })
            .OrderByDescending(x => x.Goals)
            .ThenBy(x => x.PlayerName)
            .ToList();

        return grouped;
    }
}
}