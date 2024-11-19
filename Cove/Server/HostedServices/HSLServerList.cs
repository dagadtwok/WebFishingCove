using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Steamworks;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;


namespace Cove.Server.HostedServices
{
    class RequestBody
    {
        public string host { get; set; }
        public string lobby_code { get; set; }
        public string version { get; set; }
        public string lobby_type { get; set; }
        public int player_cap { get; set; }
        public bool age_restricted { get; set; }
        public string map { get; set; }
        public string title { get; set; }
        public string[] mods { get; set; }
        public string country { get; set; }
        public int current_players { get; set; }
    }

    public class HLSServerListService : IHostedService, IDisposable
    {
        private readonly ILogger<HLSServerListService> _logger;
        private Timer _timer;
        private CoveServer server;

        public HLSServerListService(ILogger<HLSServerListService> logger, CoveServer server)
        {
            _logger = logger;
            this.server = server;
        }

        // This method is called when the service is starting.
        // the timeout for servers is 3 minutes, so we are going to send a heartbeat every 2.45 minutes
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HLSServerListService is starting.");

            // Setup a timer to trigger the task periodically.
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(45));

            return Task.CompletedTask;
        }

        // This is the method that will be triggered periodically by the timer.
        private void DoWork(object state)
        {
            _logger.LogInformation("HLSServerListService is working.");

            string endpoint = $"https://hooklinesinker.lol/servers"; // api endpoint for HLS server list
            using (HttpClient client = new HttpClient())
            {
                var requestBody = new RequestBody
                {
                    host = SteamFriends.GetPersonaName(),
                    lobby_code = server.LobbyCode,
                    version = server.WebFishingGameVersion,
                    lobby_type = server.codeOnly ? "Code Only" : "Public",
                    player_cap = server.MaxPlayers,
                    age_restricted = server.ageRestricted,
                    map = "default", // make this changeable later (will add map id to server config)
                    title = server.ServerName,
                    mods = new string[] { }, // again make this changeable later
                    country = SteamUtils.GetIPCountry(), // get the country of the server
                    current_players = server.getAllPlayers().Count() - 1 // -1 because the host is not included in the player count
                };

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                };

                string jsonBody = JsonSerializer.Serialize(requestBody, options);
                HttpContent content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                try
                {
                    // send a heartbeat to the HLS server list
                    HttpResponseMessage response = client.PostAsync(endpoint, content).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Heartbeat sent to HLS server list.");
                    }
                    else
                    {
                        _logger.LogError("Failed to send heartbeat to HLS server list.");
                        _logger.LogError(response.Content.ReadAsStringAsync().Result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send heartbeat to HLS server list.");
                }

            }
        }

        // This method is called when the service is stopping.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HLSServerListService is stopping.");

            // Stop the timer and dispose of it.
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        // This method is called to dispose of the resources.
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

}
