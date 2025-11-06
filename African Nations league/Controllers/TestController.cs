using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using African_Nations_league.Services;

public class TestController : Controller
{
    private readonly IEmailService _email;

    public TestController(IEmailService email)
    {
        _email = email;
    }

    [HttpGet("/test-email")]
    public async Task<IActionResult> TestEmail()
    {
        var to = "kibambostephane@gmail.com"; // remplace par ton email
        var subject = "Test SendGrid - African Nations League";
        var plain = "Hello — this is a test email from the local app.";
        var html = "<p>Hello — this is a <strong>test</strong> email from the local app.</p>";

        var ok = await _email.SendEmailAsync(to, subject, plain, html);
        return Content(ok ? "Email sent OK" : "Email failed to send. Check logs / config.");
    }
}