namespace African_Nations_league.Models
{
    public class LeaderboardEntry
    {
        public string PlayerName { get; set; }
        public string TeamName { get; set; }
        public string TeamFlag { get; set; }     // url du drapeau
        public string PlayerPicture { get; set; } // chemin / url de la photo (Players.ImagePath)
        public int Goals { get; set; }
    }
}