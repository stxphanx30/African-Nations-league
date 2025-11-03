
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace African_Nations_league.Models
    {
      
         public class Teams
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }  // MongoDB ObjectId

        public string TeamId { get; set; }  // SportMonks ID, e.g. "18549"

        public string TeamName { get; set; }
        public string TeamCode { get; set; }
        public string FlagUrl { get; set; }
        public double TeamRating { get; set; }
        public List<Players> Players { get; set; } = new List<Players>();
    }
}


