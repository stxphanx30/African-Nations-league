using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace African_Nations_league.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; } // "Admin" or "Representative"

        // For representatives
        public string TeamId { get; set; }      // Team MongoDB Id
        public string TeamName { get; set; }
        public string TeamFlag { get; set; }
        public string ManagerName { get; set; }
        public double TeamRating { get; set; }  // <- new
        public List<Players> Squad { get; set; } = new List<Players>();

    }
}
