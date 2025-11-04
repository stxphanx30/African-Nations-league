using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace African_Nations_league.Models
{
    public class Fixture
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string TeamAId { get; set; }
        public string TeamAName { get; set; }
        public string TeamBId { get; set; }
        public string TeamBName { get; set; }

        public int ScoreA { get; set; } = 0;
        public int ScoreB { get; set; } = 0;

        public string Status { get; set; } = "Scheduled"; // Scheduled, Playing, Finished
        public string Phase { get; set; } // <--- Ajouter ceci
        public List<GoalEvent> Events { get; set; } = new List<GoalEvent>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class GoalEvent
    {
        public string PlayerName { get; set; }
        public int Minute { get; set; }
        public string TeamName { get; set; }
    }
}
