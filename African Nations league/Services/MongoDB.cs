using MongoDB.Driver;
using African_Nations_league.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<Teams> _teams;

        public MongoDbService(IConfiguration configuration)
        {
            // Récupère la connection string et le nom de la DB depuis appsettings.json
            var connectionString = configuration.GetConnectionString("MongoDb");
            var databaseName = configuration.GetValue<string>("MongoDbSettings:DatabaseName");

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _teams = database.GetCollection<Teams>("teams");
        }

        public async Task InsertTeamAsync(Teams team)
        {
            await _teams.InsertOneAsync(team);
        }

        public async Task<List<Teams>> GetAllTeamsAsync()
        {
            return await _teams.Find(t => true).ToListAsync();
        }
    }
}
