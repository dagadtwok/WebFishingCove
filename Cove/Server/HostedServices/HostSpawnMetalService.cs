using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Steamworks;
namespace Cove.Server.HostedServices
{
    public class HostSpawnMetalService : IHostedService, IDisposable
    {
        private readonly ILogger<HostSpawnMetalService> _logger;
        private Timer _timer;
        private CoveServer server;

        public HostSpawnMetalService(ILogger<HostSpawnMetalService> logger, CoveServer server)
        {
            _logger = logger;
            this.server = server;
        }

        // This method is called when the service is starting.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnMetalService is starting.");

            // Setup a timer to trigger the task periodically.
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(8));

            return Task.CompletedTask;
        }

        // This is the method that will be triggered periodically by the timer.
        private void DoWork(object state)
        {
            _logger.LogInformation("HostSpawnMetalService is working.");

            // still got no idea
            //server.gameLobby.SetData("server_browser_value", "0");
            SteamMatchmaking.SetLobbyData(server.SteamLobby, "server_browser_value", "0");

            int metalCount = server.serverOwnedInstances.FindAll(a => a.Type == "metal_spawn").Count;
            if (metalCount > 7)
                return;

            if (server.shouldSpawnMetal)
                server.spawnMetal();

        }

        // This method is called when the service is stopping.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnMetalService is stopping.");

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
