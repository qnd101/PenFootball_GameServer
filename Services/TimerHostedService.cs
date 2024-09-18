using Microsoft.AspNetCore.SignalR;
using PenFootball_GameServer.Hubs;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PenFootball_GameServer.Services
{    
    public class TimerHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<TimerHostedService> _logger;
        private Timer? _timer = null;
        private readonly IServiceScopeFactory _scopeFactory;
        private IGameDataService _gamedata;
        private IPosterService _poster;
        private Stopwatch stopwatch = new Stopwatch();
        int counter = 0;

        public float Interval { get; private set; }

        public TimerHostedService(ILogger<TimerHostedService> logger, IServiceScopeFactory scopeFactory, IGameDataService gamedata, IPosterService poster)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _gamedata = gamedata;
            _poster = poster;
            Interval = 0.03f; // 30fps
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

            //_timer = new Timer(DoWork, null, (int)(1000*Interval),
            //    Timeout.Infinite);
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(Interval));
            stopwatch.Start();

            return Task.CompletedTask;
        }
        
        private string? funcnamefromoutput(IGameOutput op)
        {
            switch (op)
            {
                case PreviewOutput:
                    return "Preview";
                case GameEndOutput:
                    return "GameEnd";
                case ChatOutput:
                    return "Chat";
                case ScoreOutput:
                    return "Score";
                case WaitingInfoOutput:
                    return "WaitingInfo";
                case GameFoundOutput:
                    return "GameFound";
            }
            return null;
        }

        private async void DoWork(object? state)
        {
            stopwatch.Start();
            //나중에 Asynchronous한 작업으로 수정하는 것도 좋을 듯. 
            try {        
                _gamedata.MakeMatches(Interval);
                _gamedata.Update(Interval);
                var frames = _gamedata.AllConIDs().Select(conid => (conid, _gamedata.GetFrame(conid))).Where(x => x.Item2 != null);
                var outputs = _gamedata.AllConIDs().SelectMany(conid => _gamedata.GetOutputs(conid).Select(output => (conid, output)));
                var results = _gamedata.GetGameResults();
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedcontext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                    await Task.WhenAll(frames.Select(item => scopedcontext.Clients.Client(item.conid).SendAsync("UpdateFrame", item.Item2)));
                    await Task.WhenAll(outputs.Select(item =>
                        (funcnamefromoutput(item.output) is string str) ? scopedcontext.Clients.Client(item.conid).SendAsync(str, item.output) : Task.CompletedTask));
                    await Task.WhenAll(results.Select(item => _poster.PostJSON("api/servers/gameresult", item)));
                }
                foreach (var gameid in _gamedata.AllGameIDs())
                    _gamedata.FlushOutputs(gameid);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            stopwatch.Stop();
            stopwatch.Reset();

            if (_timer != null)
                _timer.Change(Math.Max((int)(1000 * Interval) - stopwatch.ElapsedMilliseconds, 1), Timeout.Infinite);
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
