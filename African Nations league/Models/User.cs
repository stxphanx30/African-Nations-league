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

        // Pour les représentants
        public string TeamId { get; set; }      // L'ID de l'équipe choisie
        public string TeamName { get; set; }    // Nom de l'équipe
        public string TeamFlag { get; set; }    // URL du drapeau
        public List<Players> Squad { get; set; } = new List<Players>();

    }
}
