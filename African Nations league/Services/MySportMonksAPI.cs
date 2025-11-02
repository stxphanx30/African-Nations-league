using African_Nations_league.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class SportMonksService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public SportMonksService(HttpClient httpClient, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["SPORTMONKS_API_KEY"];
        }

        public async Task<List<Players>> GetPlayersByTeamIdAsync(int teamId)
        {
            var url = $"https://api.sportmonks.com/v3/football/squads/teams/{teamId}?api_token={_apiKey}&include=team;player;position;detailedPosition;player.statistics.details.type";
            var response = await _httpClient.GetFromJsonAsync<SportMonksPlayerResponse>(url);

            var players = new List<Players>();

            if (response?.Data != null)
            {
                foreach (var s in response.Data)
                {
                    var p = s;
                    var positionName = p.Position?.Name ?? "Unknown";
                    var detailed = p.Detailedposition?.Name ?? positionName;
                    var teamName = p.Team?.Name ?? "Unknown Team";
                    var teamFlag = p.Team?.ImagePath ?? "";

                    int rating = 0;

                    // on tente de récupérer la statistique rating même si certaines valeurs sont null
                    if (p.PlayerStatistics != null)
                    {
                        foreach (var stat in p.PlayerStatistics)
                        {
                            if (stat != null && stat.TypeId == 118)
                            {
                                // si Average existe, on convertit, sinon on garde 0
                                rating = stat.Value?.Average != null
                                    ? (int)Math.Round((stat.Value.Average.Value / 10.0) * 100)
                                    : 0;
                                break; // on prend la première trouvée
                            }
                        }
                    }

                    // si rating est toujours 0, fallback généré
                    if (rating == 0)
                        rating = Players.GenerateRating(positionName);

                    players.Add(new Players
                    {
                        PlayerName = p.Player?.DisplayName ?? p.Player?.Name ?? "Unknown",
                        Position = positionName,
                        DetailedPosition = detailed,
                        Rating = rating,
                        ImagePath = p.Player?.ImagePath ?? "",
                        TeamName = teamName,
                        TeamFlag = teamFlag
                    });
                }
            }

            return players.Count > 23 ? players.GetRange(0, 23) : players;
        }


        // --- DTOs minimal pour JSON mapping (on mappe que ce dont on a besoin) ---
        private class SportMonksPlayerResponse
        {
            [JsonPropertyName("data")]
            public List<SquadPlayer> Data { get; set; }
        }

        private class SquadPlayer
        {
            [JsonPropertyName("player")]
            public SMPlayer Player { get; set; }

            [JsonPropertyName("position")]
            public SMPosition Position { get; set; }

            [JsonPropertyName("detailedposition")]
            public SMDetailedPosition Detailedposition { get; set; }

            [JsonPropertyName("team")]
            public SMTeam Team { get; set; }

            // some endpoints include statistics nested under player -> we support player.statistics.details
            [JsonPropertyName("player_statistics")]
            public List<PlayerStatistic> PlayerStatistics { get; set; }
        }

        private class SMPlayer
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("display_name")] public string DisplayName { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("image_path")] public string ImagePath { get; set; }
            // other fields omitted
        }

        private class SMPosition
        {
            [JsonPropertyName("name")] public string Name { get; set; }
        }

        private class SMDetailedPosition
        {
            [JsonPropertyName("name")] public string Name { get; set; }
        }

        private class SMTeam
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("image_path")] public string ImagePath { get; set; }
        }

        // minimal structure for stat value
        private class PlayerStatistic
        {
            [JsonPropertyName("type_id")]
            public int TypeId { get; set; }

            [JsonPropertyName("value")]
            public StatValue Value { get; set; }
        }

        private class StatValue
        {
            [JsonPropertyName("average")]
            public double? Average { get; set; }
            [JsonPropertyName("highest")]
            public double? Highest { get; set; }
            [JsonPropertyName("lowest")]
            public double? Lowest { get; set; }
        }
    }
}
