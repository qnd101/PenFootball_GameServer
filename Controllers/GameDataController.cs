using Microsoft.AspNetCore.Mvc;
using PenFootball_GameServer.Hubs;
using PenFootball_GameServer.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PenFootball_GameServer.Controllers
{
    //게임 정보와 관련된 것들을 다루는 API
    //GameDataService안에 저장된 데이터를 API로 제공하는 기능
    //누가 어떤 게임을 하고 있는지
    [Route("api/[controller]")]
    [ApiController]
    public class GameDataController : ControllerBase
    {
        private IGameDataService _gamedata;
        private ILogger<GameDataController> _logger;    
        public GameDataController(IGameDataService gamedata, ILogger<GameDataController> logger) 
        { 
            _gamedata = gamedata;
            _logger = logger;
        }

        // GET api/<ValuesController>/5
        [HttpGet("players/state")]
        public async Task<IActionResult> GetPlayerStates(int id)
        {
            if(!ModelState.IsValid)
                return BadRequest(ModelState);

            //_logger.LogInformation($"API call for states with id={id}");

            if(_gamedata.GetConID(id) is string conID)
            {
                var state = _gamedata.GetPlayerState(conID);
                switch (state)
                {
                    case WaitingState ws: return Ok(new { state = "waiting" });
                    case NormGameState ngs:
                        var op = _gamedata.GetDBID(ngs.OppID);
                        if (op is int opid)
                            return Ok(new { state = "normalgame", verses = op });
                        else
                            return Ok(new { state = "normalgame" });
                    case TrainingState ts:
                        return Ok(new { state = "training" });
                    default:
                        return Ok(new { state = "unknown" });
                }
            }
            return Ok(new { state = "offline" });
        }

        //연결된 사람들의 수
        [HttpGet("summary")]
        public async Task<IActionResult> GetConnectionSummary()
        {
            int connections = GameHub._connections.Count;
            int normgames = _gamedata.Roomdata_NormGame.Count * 2;
            int twovtwos = _gamedata.Roomdata_TwoVTwo.Count * 4;
            int training = _gamedata.Roomdata_Train.Count;
            int waitings = _gamedata.Waitline_Normgame.Count + _gamedata.Waitline_TwoVTwo.Count;
            return Ok(new {connections= connections, normgames = normgames, twovtwos=twovtwos, training=training, waitings=waitings});
        }
    }
}
