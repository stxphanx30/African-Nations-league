using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace African_Nations_league.Models
{
    public class Match
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Stage { get; set; }          // e.g., "Quarter Final"
        public string HomeTeamId { get; set; }
        public string HomeTeamName { get; set; }
        public string HomeTeamFlag { get; set; }
        public int? HomeScore { get; set; }

        public string AwayTeamId { get; set; }
        public string AwayTeamName { get; set; }
        public string AwayTeamFlag { get; set; }
        public int? AwayScore { get; set; }

        public DateTime? MatchDate
        {
            get; set;
        }
    }
}