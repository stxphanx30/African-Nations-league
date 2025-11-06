using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public SendGridEmailService(IConfiguration config)
        {
            _apiKey = config["SENDGRID_API_KEY"];
            _fromEmail = config["EMAIL_FROM"] ?? "kibambostephane@gmail.com";
            _fromName = config["EMAIL_FROM_NAME"] ?? "African Nations League";
        }
        

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string plainTextContent, string htmlContent = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("SendGrid API key is missing. Configure SENDGRID_API_KEY.");

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent ?? plainTextContent);

            var response = await client.SendEmailAsync(msg);
            return response.IsSuccessStatusCode;
        }
    }
}