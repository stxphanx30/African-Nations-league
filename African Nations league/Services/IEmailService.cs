using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string plainTextContent, string htmlContent = null);
    }
}