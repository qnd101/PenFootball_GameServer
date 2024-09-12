using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PenFootball_GameServer.GameLogic;
using PenFootball_GameServer.Services;

namespace PenFootball_GameServer.Hubs
{

    [Authorize]
    public class GameHub : Hub<IGameClient>
    {
        private ILogger<GameHub> _logger;
        private IGameDataService _gamedata;
        public GameHub(ILogger<GameHub> logger, IGameDataService gamedata)
        {
            _logger = logger;
            _gamedata = gamedata;
        }

        public async Task KeyEvent(string eventtype, string keytype)
        {
            //_logger.LogInformation($"ID: {Context.ConnectionId}, Event Type: {eventtype}, Key Type: {keytype}");

            var conid = Context.ConnectionId;
            if (conid == null)
                return;

            var key = (GameKey)Enum.Parse(typeof(GameKey), keytype);
            var keyeventtype = (KeyEventType)Enum.Parse(typeof(KeyEventType), eventtype);
            _gamedata.KeyInput(conid, key, keyeventtype);   
        }

        public async Task EnterNormGame(int rating)
        {
            var conid = Context.ConnectionId;

            if (conid == null)
                return;
            var dbid = getID();
            _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) entering normgame");

            //check for double connection
            if (_gamedata.GetConID(dbid) != null)
            {
                _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) rejected for double connection while entering for normgame");
                throw new HubException("Double Connection not allowed!");
            }

            _gamedata.EnterNormGame(conid, getID(), rating);
        }

        public async Task EnterTraining()
        {
            var conid = Context.ConnectionId;
            if (conid == null)
                return;
            var dbid = getID();
            _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) entering train");

            //check for double connection
            if (_gamedata.GetConID(dbid) != null)
            {
                _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) rejected for double connection while entering train");
                throw new HubException("Double Connection not allowed!");
            }

            _gamedata.EnterTrain(conid, getID());
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation($"Connection between ID: {Context.ConnectionId} Initiated");
            if (int.TryParse(Context.User?.FindFirst(c => c.Type == "sub")?.Value, out int id))
            {
                _logger.LogInformation($"ID : {id} found from token");
                Context.Items.Add("Id", id);
            }
            else
                throw new HubException("Invalid Token!");
            return base.OnConnectedAsync();
        }

        private int getID()
        {
            if (Context.Items["Id"] is int id)
                return id;
            else
                throw new HubException("Something went wrong. Id not found");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Connection between ID: {Context.ConnectionId}({getID()}) Ended");
            _gamedata.ExitInput(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task Exit()
        {
            _logger.LogInformation($"Player ID: {Context.ConnectionId}({getID()}) exited game");
            _gamedata.ExitInput(Context.ConnectionId);
        }
    }
}