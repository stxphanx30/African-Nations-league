namespace African_Nations_league.Models
{
    public class AdminDashboardViewModel
    {
        public int TeamsCount { get; set; }
        public int UsersCount { get; set; }
        public System.Collections.Generic.List<African_Nations_league.Models.Teams> Teams { get; set; }
        public System.Collections.Generic.List<African_Nations_league.Models.User> Users { get; set; }
    }
}
