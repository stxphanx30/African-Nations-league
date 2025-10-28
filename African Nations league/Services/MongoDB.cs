using African_Nations_league.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<Teams> _teams;

        public MongoDbService(IConfiguration config)
        {
            var connectionString = config["MONGO_URI"];
            var dbName = config["MONGO_DBNAME"];
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbName);
            _teams = database.GetCollection<Teams>("teams");
        }

        public async Task InsertTeamAsync(Teams team)
        {
            await _teams.InsertOneAsync(team);
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

        public async Task<long> CountTeamsAsync()
        {
            return await _teams.CountDocumentsAsync(FilterDefinition<Teams>.Empty);
        }    

        public async Task<Teams> GetTeamByIdAsync(string id)
        {
            return await _teams.Find(t => t.Id == id).FirstOrDefaultAsync();
        }
        public async Task EnsureIndexesAsync()
{
    var indexKeys = Builders<Teams>.IndexKeys.Ascending(t => t.TeamName);
    var indexModel = new CreateIndexModel<Teams>(indexKeys, new CreateIndexOptions { Unique = true, Name = "ux_teamname" });
    await _teams.Indexes.CreateOneAsync(indexModel);
}

       
    }
}
