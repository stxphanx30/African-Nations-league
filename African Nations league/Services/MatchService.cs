using African_Nations_league.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace African_Nations_league.Services
{
    // Simple match DTO used by UI
    public class Match
    {
        public string Id { get; set; }
        public string Stage { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public string HomeTeamName { get; set; }
        public string AwayTeamName { get; set; }
        public bool Played => HomeScore.HasValue || AwayScore.HasValue;
    }

    // Very simple MatchService stub; later we'll implement simulate logic and DB persistence
    public class MatchService
    {
        private readonly MongoDbService _mongo;

        public MatchService(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        public Task<IEnumerable<Match>> GetFixturesByTeamIdAsync(string teamId)
        {
            // For now return empty list. We'll implement DB read in next steps.
            return Task.FromResult(Enumerable.Empty<Match>());
        }

        public Task<Match> SimulateMatchAsync(string matchId)
        {
            // placeholder - return null for now
            return Task.FromResult<Match>(null);
        }
    }
}
