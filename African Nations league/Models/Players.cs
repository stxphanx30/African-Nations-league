namespace African_Nations_league.Models
{
    public class Players
    {
        public string PlayerName { get; set; }
        public string TeamName { get; set; }
        public string TeamFlag { get; set; }
        public string Position { get; set; }
        public string DetailedPosition { get; set; }
        public int Rating { get; set; }
        public string ImagePath { get; set; }

        public static int GenerateRating(string position)
        {
            var rnd = new System.Random();
            return position?.ToLower() switch
            {
                "attacker" => rnd.Next(70, 96),
                "midfielder" => rnd.Next(65, 91),
                "defender" => rnd.Next(60, 86),
                "goalkeeper" => rnd.Next(65, 91),
                _ => rnd.Next(60, 91)
            };
        }
    }
}
