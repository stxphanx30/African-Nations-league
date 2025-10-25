namespace African_Nations_league.Services
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class FootballApiClient
    {
        private readonly HttpClient _httpClient;

        public FootballApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> GetPlayerDataAsync(string url)
        {
            var response = await _httpClient.GetStringAsync(url);
            return response;
        }
    }

}
