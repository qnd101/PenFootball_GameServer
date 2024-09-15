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
        private EntranceSettings _entrancesettings;
        public GameHub(ILogger<GameHub> logger, IGameDataService gamedata, EntranceSettings entranceSettings)
        {
            _logger = logger;
            _gamedata = gamedata;
            _entrancesettings = entranceSettings;
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
            if (!_entrancesettings.Validate(Context.Items.ToDictionary()))
                throw new HubException("You are not allowed in this server!!");

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

            var logstr = "";
            if (!int.TryParse(Context.User?.FindFirst(c => c.Type == "sub")?.Value, out int id))
                throw new HubException("Invalid Token!");
            var email = Context.User?.FindFirst(c => c.Type == "email")?.Value ?? throw new HubException("Invalid Token!");
            _logger.LogInformation($"ID = {id}, Email = {email} found from token");
            Context.Items.Add("ID", id);
            Context.Items.Add("Email", id);
            return base.OnConnectedAsync();
        }

        private int getID()
        {
            if (Context.Items["ID"] is int id)
                return id;
            else
                throw new HubException($"Something went wrong. ID not found");
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