namespace African_Nations_league.Models
{
    public class SignUpViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PhoneNumber { get; set; }

        // Representative fields
        public string ManagerName { get; set; }
        public string TeamId { get; set; } // selected team
    }

}
