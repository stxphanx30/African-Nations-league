namespace African_Nations_league.Models
{
    public class MatchPairViewModel
    {
        public string HomeTeamId { get; set; }
        public string HomeTeamName { get; set; }
        public double HomeTeamRating { get; set; }

        public string AwayTeamId { get; set; } // nullable if no opponent yet
        public string AwayTeamName { get; set; }
        public double AwayTeamRating { get; set; }
    }
}
