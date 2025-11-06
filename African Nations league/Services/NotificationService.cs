using African_Nations_league.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class NotificationService
    {
        private readonly IEmailService _email;
        private readonly MongoDbService _mongo; // ton service mongo

        public NotificationService(IEmailService emailService, MongoDbService mongo)
        {
            _email = emailService;
            _mongo = mongo;
        }

        /// <summary>
        /// Notify users whose TeamId matches TeamA or TeamB for a finished fixture.
        /// </summary>
        public async Task NotifyUsersAboutMatchResult(Fixture fixture)
        {
            if (fixture == null) return;

            // get users once (or query by team ids if you have that method)
            var users = await _mongo.GetAllUsersAsync();
            var interested = users.Where(u => u.NotifyByEmail &&
                                    (u.TeamId == fixture.TeamAId || u.TeamId == fixture.TeamBId))
                                  .ToList();

            if (!interested.Any()) return;

            string phaseLabel = string.IsNullOrEmpty(fixture.Phase) ? "Quarter-finals" : fixture.Phase;

            // determine winner
            string winnerId = null;
            if (fixture.ScoreA > fixture.ScoreB) winnerId = fixture.TeamAId;
            else if (fixture.ScoreB > fixture.ScoreA) winnerId = fixture.TeamBId;

            foreach (var u in interested)
            {
                bool isTeamA = u.TeamId == fixture.TeamAId;
                string teamName = isTeamA ? fixture.TeamAName : fixture.TeamBName;
                string opponentName = isTeamA ? fixture.TeamBName : fixture.TeamAName;
                string teamFlag = isTeamA ? fixture.TeamAFlag : fixture.TeamBFlag;
                string teamScore = isTeamA ? fixture.ScoreA.ToString() : fixture.ScoreB.ToString();
                string oppScore = isTeamA ? fixture.ScoreB.ToString() : fixture.ScoreA.ToString();

                string subject;
                string plain;
                string html;

                if (winnerId == null)
                {
                    subject = $"Match result: {teamName} {teamScore} - {oppScore} {opponentName}";
                    plain = $"Hi {u.FullName},\n\nThe match between {teamName} and {opponentName} ended {teamScore}-{oppScore} in the {phaseLabel}.\n\nBest regards,\nAfrican Nations League";
                    html = $"<p>Hi {System.Net.WebUtility.HtmlEncode(u.FullName)},</p>" +
                           $"<p>The match <strong>{System.Net.WebUtility.HtmlEncode(teamName)} vs {System.Net.WebUtility.HtmlEncode(opponentName)}</strong> ended <strong>{teamScore} - {oppScore}</strong> in the <strong>{System.Net.WebUtility.HtmlEncode(phaseLabel)}</strong>.</p>" +
                           $"<p>Best regards,<br/>African Nations League</p>";
                }
                else if ((isTeamA && winnerId == fixture.TeamAId) || (!isTeamA && winnerId == fixture.TeamBId))
                {
                    // user's team WON
                    subject = $"Congratulations — {teamName} won {teamScore}-{oppScore}!";
                    plain = $"Hi {u.FullName},\n\nCongratulations — your team {teamName} beat {opponentName} {teamScore}-{oppScore} in the {phaseLabel}. They advance to the next round.\n\nBest regards,\nAfrican Nations League";
                    html = $@"<p>Hi {System.Net.WebUtility.HtmlEncode(u.FullName)},</p>
                              <p>Congratulations — your team <strong>{System.Net.WebUtility.HtmlEncode(teamName)}</strong> beat <strong>{System.Net.WebUtility.HtmlEncode(opponentName)}</strong> <strong>{teamScore} - {oppScore}</strong> in the <strong>{System.Net.WebUtility.HtmlEncode(phaseLabel)}</strong>.</p>
                              <p>They advance to the next round. 🎉</p>
                              {(string.IsNullOrEmpty(teamFlag) ? "" : $"<p><img src=\"{teamFlag}\" alt=\"{System.Net.WebUtility.HtmlEncode(teamName)} flag\" style=\"width:120px;border-radius:6px;\" /></p>")}
                              <p>Best regards,<br/>African Nations League</p>";
                }
                else
                {
                    // user's team LOST
                    subject = $"Result — {teamName} lost {teamScore}-{oppScore}";
                    plain = $"Hi {u.FullName},\n\nWe’re sorry — your team {teamName} lost to {opponentName} {teamScore}-{oppScore} in the {phaseLabel}.\n\nBest regards,\nAfrican Nations League";
                    html = $@"<p>Hi {System.Net.WebUtility.HtmlEncode(u.FullName)},</p>
                              <p>We’re sorry — your team <strong>{System.Net.WebUtility.HtmlEncode(teamName)}</strong> lost to <strong>{System.Net.WebUtility.HtmlEncode(opponentName)}</strong> <strong>{teamScore} - {oppScore}</strong> in the <strong>{System.Net.WebUtility.HtmlEncode(phaseLabel)}</strong>.</p>
                              {(string.IsNullOrEmpty(teamFlag) ? "" : $"<p><img src=\"{teamFlag}\" alt=\"{System.Net.WebUtility.HtmlEncode(teamName)} flag\" style=\"width:120px;border-radius:6px;\" /></p>")}
                              <p>Thank you for supporting them.<br/>Best regards,<br/>African Nations League</p>";
                }

                try
                {
                    await _email.SendEmailAsync(u.Email, subject, plain, html);
                }
                catch (Exception ex)
                {
                    // log and continue (do not throw)
                    // e.g. _logger.LogError(ex, "Failed to send match email to {Email}", u.Email);
                }
            }
        }

        /// <summary>
        /// Notify winners (winners list from previous phase) that they qualified to nextPhase.
        /// winners: list of (TeamId, TeamName)
        /// </summary>
        public async Task NotifyWinnersQualified(List<(string TeamId, string TeamName)> winners, string nextPhase)
        {
            if (winners == null || winners.Count == 0) return;

            var users = await _mongo.GetAllUsersAsync();
            foreach (var w in winners)
            {
                var teamUsers = users.Where(u => u.NotifyByEmail && u.TeamId == w.TeamId).ToList();
                if (!teamUsers.Any()) continue;

                string subject = $"{w.TeamName} qualified to {nextPhase}";
                string plain = $"Hi {{0}},\n\nYour team {w.TeamName} has qualified to {nextPhase}.\n\nBest regards,\nAfrican Nations League";
                string html = $"<p>Hi {{0}}</p><p>Great news — <strong>{System.Net.WebUtility.HtmlEncode(w.TeamName)}</strong> has qualified to <strong>{System.Net.WebUtility.HtmlEncode(nextPhase)}</strong>!</p><p>Best regards,<br/>African Nations League</p>";

                foreach (var u in teamUsers)
                {
                    try
                    {
                        await _email.SendEmailAsync(u.Email, subject, string.Format(plain, u.FullName), string.Format(html, System.Net.WebUtility.HtmlEncode(u.FullName)));
                    }
                    catch (Exception ex)
                    {
                        // log and continue
                    }
                }
            }
        }
    }
}