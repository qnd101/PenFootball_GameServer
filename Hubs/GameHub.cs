using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PenFootball_GameServer.GameLogic;
using PenFootball_GameServer.Services;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

namespace PenFootball_GameServer.Hubs
{
    public record ChatObj(string name, string msg, string time);

    [Authorize]
    public class GameHub : Hub<IGameClient>
    {
        private ILogger<GameHub> _logger;
        private IGameDataService _gamedata;
        private EntranceSettings _entrancesettings;
        private IGlobalChatService _globalchatservice;

        public GameHub(ILogger<GameHub> logger, IGameDataService gamedata, EntranceSettings entranceSettings, IGlobalChatService globalchatservice)
        {
            _logger = logger;
            _gamedata = gamedata;
            _entrancesettings = entranceSettings;
            _globalchatservice = globalchatservice;
        }

        //결과에 따른 status code 반환
        public async Task<int> GlobalChat(string msg)
        {
            string name = (Context.Items["Name"] as string) ?? throw (new HubException("Name not found"));
            var result = _globalchatservice.AddChat(name, msg);
            if(result == ChatResult.Success) 
                await Clients.All.GlobalChat(new ChatObj(name, msg, $"{DateTime.Now.Hour}:{DateTime.Now.Minute}"));
            return (int)result;
        }

        public async Task<ChatObj[]> GetGlobalChatCache()
        {
            return _globalchatservice.GetCache();
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

        private Task validateEntrance()
        {
            var data = Context.Items.ToDictionary(
                kvp => kvp.Key?.ToString() ?? string.Empty,
                kvp => kvp.Value?.ToString() ?? string.Empty);
            _logger.LogInformation(string.Concat(data.Select(item => item.Key + ":" + item.Value + "\n")));
            if (!_entrancesettings.Validate(data))
                throw new HubException("You are not allowed in this server!!");
            return Task.CompletedTask;
        }

        public async Task EnterNormGame(int rating)
        {
            await validateEntrance();
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
        public async Task EnterTwoVTwo(int rating)
        {
            await validateEntrance();

            var conid = Context.ConnectionId;

            if (conid == null)
                return;
            var dbid = getID();
            _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) entering 2 vs 2");

            //check for double connection
            if (_gamedata.GetConID(dbid) != null)
            {
                _logger.LogInformation($"ID: {Context.ConnectionId}({dbid}) rejected for double connection while entering 2 vs 2");
                throw new HubException("Double Connection not allowed!");
            }

            _gamedata.EnterTwoVTwo(conid, getID(), rating);
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
            string email = Context.User?.FindFirst(c => c.Type == "email")?.Value ?? throw new HubException("Invalid Token!");

            string name = Context.User?.FindFirst(c => c.Type == JwtRegisteredClaimNames.Name)?.Value ?? throw new HubException("Invalid Token!");

            _logger.LogInformation($"ID = {id}, Email = {email}, Name = {name} found from token");

            Context.Items.Add("ID", id);
            Context.Items.Add("Email", email);
            Context.Items.Add("Name", name);
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