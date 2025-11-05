namespace African_Nations_league.Models
{
    public class BracketMatchViewModel
    {
        public string FixtureId { get; set; }
        public string Phase { get; set; }            // "Quarts de finale", "Demi-finales", "Finale"
        public string TeamAId { get; set; }
        public string TeamAName { get; set; }
        public string TeamAFlag { get; set; }
        public int ScoreA { get; set; }

        public string TeamBId { get; set; }
        public string TeamBName { get; set; }
        public string TeamBFlag { get; set; }
        public int ScoreB { get; set; }

        public string Status { get; set; }          // Scheduled, Waiting, Simulated, Played, Finished
        public List<GoalEvent> Events { get; set; } = new();
    }

    public class BracketViewModel
    {
        public List<BracketMatchViewModel> QuarterMatches { get; set; } = new();
        public List<BracketMatchViewModel> SemiMatches { get; set; } = new();
        public BracketMatchViewModel FinalMatch { get; set; }
        public string Champion { get; set; } = null;
    }
}