using African_Nations_league.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class SportMonksService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "89BDoNcyoPL6UPCi3nEtEj87PPl0kiqrY1YrmYOlXhsfVZxo8g26xFJNzEJy";

        public SportMonksService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Récupère les joueurs d'une équipe par TeamId
        public async Task<List<Players>> GetPlayersByTeamIdAsync(int teamId)
        {
            var response = await _httpClient.GetFromJsonAsync<SportMonksPlayerResponse>(
                $"https://api.sportmonks.com/v3/football/squads/teams/{teamId}?api_token={_apiKey}&include=player;position;detailedPosition"
            );

            var players = new List<Players>();

            if (response?.Data != null)
            {
                foreach (var p in response.Data)
                {
                    var positionName = p.Position?.Name ?? "Unknown";
                    var teamName = p.Teams?.TeamName ?? "Unknown Team";
                    var teamFlag = p.Teams?.FlagUrl ?? "";
                    players.Add(new Players
                    {
                        PlayerName = p.Player.display_name ?? "Unknown",
                        Position = positionName,
                        DetailedPosition = p.Detailedposition?.Name ?? positionName,
                        Rating = Players.GenerateRating(positionName),
                        ImagePath = p.Player.ImagePath ?? "",
                        TeamName = teamName,
                        TeamFlag = teamFlag
                    });
                }
            }

            // Limiter à 23 joueurs pour la sélection type
            return players.Count > 23 ? players.GetRange(0, 23) : players;
        }

        // Génère un rating aléatoire selon la position
        private int GenerateRating(string position)
        {
            Random rnd = new Random();
            return position?.ToLower() switch
            {
                "attacker" => rnd.Next(70, 96),
                "midfielder" => rnd.Next(65, 91),
                "defender" => rnd.Next(60, 86),
                "goalkeeper" => rnd.Next(65, 91),
                _ => rnd.Next(60, 91)
            };
        }

        // Classes pour désérialisation JSON
        private class SportMonksPlayerResponse
        {
            public List<SquadPlayer> Data { get; set; }
        }

        private class SquadPlayer
        {
            public SportMonksPlayer Player { get; set; }
            public Position Position { get; set; }
            public DetailedPosition Detailedposition { get; set; }
            public Teams Teams { get; set; }
        }

        private class SportMonksPlayer
        {
            public string FullName { get; set; }
            public string Firstname { get; set; }
            public string Lastname { get; set; }
            public string display_name { get; set; }
            public string ImagePath { get; set; }
        }

        private class Position
        {
            public string Name { get; set; }
        }

        private class DetailedPosition
        {
            public string Name { get; set; }
        }
    }
}
