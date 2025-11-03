using African_Nations_league.Models;

namespace African_Nations_league.Models
{
    public class TeamDetailsViewModel
    {
        public string Id { get; set; }          // Mongo ObjectId ou User.TeamId
        public string TeamId { get; set; }      // SportMonks ID
        public string TeamName { get; set; }
        public string TeamFlag { get; set; }
        public string ManagerName { get; set; } // Only for user's team
        public double TeamRating { get; set; }

        public List<Players> Squad { get; set; } = new List<Players>();
    }
}
