using System;
using System.Threading;
using System.Threading.Tasks;
using Cove.Server.Actor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;



namespace Cove.Server.HostedServices
{
    public class HostSpawnService : IHostedService, IDisposable
    {
        private readonly ILogger<HostSpawnService> _logger;
        private Timer _timer;
        private CoveServer server;

        public HostSpawnService(ILogger<HostSpawnService> logger, CoveServer server)
        {
            _logger = logger;
            this.server = server;
        }

        // This method is called when the service is starting.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnService is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            return Task.CompletedTask;
        }

        private float rainChance = 0f;

        // This is the method that will be triggered periodically by the timer.
        private void DoWork(object state)
        {
            _logger.LogInformation("HostSpawnService is working.");

            // remove old instances!
            try
            {
                foreach (WFActor inst in server.serverOwnedInstances)
                {
                    float instanceAge = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inst.SpawnTime.ToUnixTimeSeconds();
                    if (inst.despawn && instanceAge >= inst.despawnTime)
                    {
                        server.removeServerActor(inst);
                        Console.WriteLine($"Removed {inst.Type}, Decayed");
                    }
                    Console.WriteLine($"Instance {inst.Type} is {instanceAge} seconds old");
                }
            }
            catch (Exception e)
            {
                // most of the time this is just going to be an error 
                // because the list was modified while iterating
                // casued by a actorspawn or despawn, nothing huge.
                _logger.LogError(e, "Error removing old instances");
            }

            Random ran = new Random();
            string[] beginningTypes = ["fish", "none"];
            string type = beginningTypes[ran.Next() % 2];

            if (ran.NextSingle() < 0.01 && ran.NextSingle() < 0.4)
            {
                if (server.shouldSpawnMeteor)
                    type = "meteor";
            }

            if (ran.NextSingle() < rainChance && ran.NextSingle() < .12f)
            {
                type = "rain";
                rainChance = 0;
            }
            else
            {
                if (ran.NextSingle() < .75f)
                    rainChance += .001f * server.rainMultiplyer;
            }

            if (ran.NextSingle() < 0.01 && ran.NextSingle() < 0.25)
            {
                if (server.shouldSpawnPortal)
                    type = "void_portal";
            }

            switch (type)
            {

                case "none":
                    break;

                case "fish":
                    // dont spawn too many because it WILL LAG players!
                    if (server.serverOwnedInstances.Count > 15)
                        return;
                    WFActor a = server.spawnFish();
                    break;

                case "meteor":
                    server.spawnFish("fish_spawn_alien");
                    break;

                case "rain":
                    server.spawnRainCloud();
                    break;

                case "void_portal":
                    server.spawnVoidPortal();
                    break;

            }

            Console.WriteLine($"Spawned {type}");

        }

        // This method is called when the service is stopping.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnService is stopping.");

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
