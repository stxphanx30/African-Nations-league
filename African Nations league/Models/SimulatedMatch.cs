namespace African_Nations_league.Models
{
    public class SimulatedMatch
    {
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public string Result { get; set; }
        public List<string> GoalScorers { get; set; }
    }
}
