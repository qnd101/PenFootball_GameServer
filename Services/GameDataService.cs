using PenFootball_GameServer.GameLogic;
using PenFootball_GameServer.Hubs;
using System.Collections.Immutable;
using System.ComponentModel.Design.Serialization;

namespace PenFootball_GameServer.Services
{

    //나중에 각종 통계도 추가할 것
    public record GameResultData(int Player1ID, int Player2ID, int winner);

    public interface IGameDataService
    {
        //ConID와 DBID 사이 왔다갔다
        string? GetConID(int id);
        int? GetDBID(string conid);
        IEnumerable<string> AllConIDs ();
        IEnumerable<int> AllGameIDs();

        //플레이어 상태 가져오기 / 게임 상태 가져오기
        IPlayerState? GetPlayerState(string conid);

        //입력
        void KeyInput(string conid, GameKey keytype, KeyEventType eventtype);
        void ExitInput(string conid); 

        //플레이어 추가/제거 (Double connection이면 아무 작업도 안하도록)
        void EnterTrain(string conid, int dbid); 
        void EnterNormGame (string conid, int dbid, int rating);

        //출력
        Frame? GetFrame(string conid);
        IEnumerable<IGameOutput> GetOutputs (string conid); //단발성 아웃풋
        void FlushOutputs(int gameid);
        IEnumerable<GameResultData> GetGameResults(); //Post를 위한 데이터

        //업데이트
        //매치 메이킹 -> 생성된 ID 쌍들을 반환
        IEnumerable<(string conid1, string conid2, int id1, int id2)> MakeMatches(float dt);
        void Update(float dt);

        //void AddEvent(int gameid, IGameEvent gameEvent);

        //(string conid, int id)? AddWait(string connectionid, int playerid, out bool doubleconnnection);

        //void RemoveID(string conid);

        //(IEnumerable<(string connectionid, Frame frame)>, IEnumerable<GameResultData>) UpdateGames(float dt);
    }

    public class GameDataService : IGameDataService
    {
        private ImmutableDictionary<int, (NormGame Game, string Player1ID, string Player2ID)> _roomdata_normgame;
        private ImmutableDictionary<int, (TrainGame Game, string PlayerID)> _roomdata_train;
        
        private ImmutableList<(string conid, int rating)> _waitline_normgame;

        //Data of Currently Playing/Waiting players
        private ImmutableDictionary<string, int> _dbiddata;

        private ILogger<GameDataService> _logger;

        private int _gameidcounter = 0;
        private float _normgamematchcounter = 10;
        private const float normgametimeout = 10;
        List<int> _removelist = new();
        object removelistlock = new();

        public GameDataService(ILogger<GameDataService> logger)
        {
            _logger = logger;
            _roomdata_normgame = ImmutableDictionary<int, (NormGame, string, string)>.Empty;
            _roomdata_train = ImmutableDictionary<int, (TrainGame, string)>.Empty;
            _waitline_normgame = ImmutableList<(string, int)>.Empty;
            _dbiddata = ImmutableDictionary<string, int>.Empty;
        }

        public string? GetConID (int id)
        {
            var items = _dbiddata.Where(item => item.Value == id);
            if (items.Any())
                return items.First().Key;
            return null;
        }

        public int? GetDBID (string conid)
        {
            if(_dbiddata.Keys.Contains(conid))
                return _dbiddata[conid];
            return null;
        }

        public IEnumerable<string> AllConIDs() => _dbiddata.Keys;
        public IEnumerable<int> AllGameIDs() => _roomdata_normgame.Keys.Concat(_roomdata_train.Keys);

        public IPlayerState? GetPlayerState(string conid)
        {
            if (_waitline_normgame.Any(x => x.conid == conid))
                return new WaitingState(GameType.NormGame);

            foreach(var item in _roomdata_train)
            {
                if (item.Value.PlayerID == conid)
                    return new TrainingState(item.Key);
            }

            foreach (var item in _roomdata_normgame)
            {
                if (item.Value.Player1ID == conid)
                    return new NormGameState(item.Value.Player2ID, 1, item.Key);
                else if (item.Value.Player2ID == conid)
                    return new NormGameState(item.Value.Player1ID, 2, item.Key);
            }

            return null;
        }

        public void KeyInput(string conid, GameKey keytype, KeyEventType eventtype)
        {
            switch (GetPlayerState(conid))
            {
                case TrainingState ts:
                    var ev1 = new KeyEvent(1, keytype, eventtype);
                    _roomdata_train[ts.GameID].Game.EventQueue.Enqueue(ev1);
                    break;
                case NormGameState ns:
                    var ev2 = new KeyEvent(ns.PlayerType, keytype, eventtype);
                    _roomdata_normgame[ns.GameID].Game.EventQueue.Enqueue(ev2);
                    break;
            }
        }

        //register에 성공했는지 여부 반환
        private bool register(string conid, int dbid)
        {
            if (!_dbiddata.Values.Contains(dbid))
            {
                ImmutableInterlocked.TryAdd(ref _dbiddata, conid, dbid);
                return true;
            }
            _logger.LogInformation($"Player ID : {dbid} make a double connection");
            return false;
        }

        public void EnterTrain(string conid, int playerid)
        {
            if (!register(conid, playerid))
                return;
            Interlocked.Increment(ref _gameidcounter);
            ImmutableInterlocked.TryAdd(ref _roomdata_train, _gameidcounter, (new TrainGame(), conid));
            _logger.LogInformation($"Train Game of ID: {_gameidcounter} Created by ID: {conid}({playerid})");
        }

        public void EnterNormGame(string conid, int dbid, int rating)
        {
            if(!register(conid, dbid))
                return;

            ImmutableInterlocked.Update(ref _waitline_normgame, (_waitline) => _waitline.Add((conid, rating)));
            _logger.LogInformation($"Player of ID: {conid}({dbid}) waiting for norgame");
        }

        //Before calling this method, one needs to be sure that the player is not in any game or waitingline
        private void unregister(string conid)
        {
            if (GetPlayerState(conid) != null)
                throw new Exception("unregister attempt while player is in game");
            ImmutableInterlocked.TryRemove(ref _dbiddata, conid, out _);
        }

        public void ExitInput(string conid)
        {
            switch(GetPlayerState(conid))
            {
                case WaitingState ws:
                    //지금 상황에서 waitline은 normgame 뿐
                    ImmutableInterlocked.Update(ref _waitline_normgame, (li) => li.RemoveAll(x => x.conid == conid));
                    unregister(conid);
                    break;
                case TrainingState ts:
                    ImmutableInterlocked.TryRemove(ref _roomdata_train, ts.GameID, out _);
                    unregister(conid);
                    //얘는 애초에 한명밖에 없으니 ExitEvent로 처리하지 않아도 됨. 바로 탈주
                    break;
                case NormGameState ns:
                    _roomdata_normgame[ns.GameID].Game.EventQueue.Enqueue(new ExitEvent(ns.PlayerType));
                    break;
            }
        }

        //conid에게 보이는 시야를 가져옴
        public Frame? GetFrame(string conid)
        {
            switch (GetPlayerState(conid))
            {
                case TrainingState ts:
                    return _roomdata_train[ts.GameID].Game.GetFrame();
                case NormGameState ns:
                    return _roomdata_normgame[ns.GameID].Game.GetFrame(ns.PlayerType);
                default:
                    return null;
            }
        }
        public IEnumerable<IGameOutput> GetOutputs(string conid)
        {
            switch (GetPlayerState(conid))
            {
                case NormGameState ns:                  
                    return _roomdata_normgame[ns.GameID].Game.GetOutputs(ns.PlayerType);
                    
                case TrainingState ts:
                    return _roomdata_train[ts.GameID].Game.GetOutputs();
                default:
                    return new List<IGameOutput>();
            }
        }

        //모든 끝난 게임들의 결과를 가져옴
        public IEnumerable<GameResultData> GetGameResults()
        {
            List<GameResultData> results = new();
            foreach (var item in _roomdata_normgame.Values)
            {
                if (item.Game.GetOutputs(1).FirstOrDefault(x=>x is GameEndOutput) is GameEndOutput go)
                    results.Add(new GameResultData(_dbiddata[item.Player1ID], _dbiddata[item.Player2ID], go.Winner));
            }
            return results;
        }

        public void FlushOutputs(int gameid)
        {
            if(_roomdata_normgame.Keys.Contains(gameid))
            {
                _roomdata_normgame[gameid].Game.FlushOutputs();
            }
            else if(_roomdata_train.Keys.Contains(gameid))
            {
                _roomdata_train[gameid].Game.FlushOutputs();
            }
        }

        public IEnumerable<(string, string, int, int)> MakeMatches(float dt)
        {
            _normgamematchcounter -= dt;
            var result = new List<(string, string, int, int)>();
            if (_normgamematchcounter < 0)
            {
                _normgamematchcounter = normgametimeout;
                var snapshot = _waitline_normgame; //현재 스냅숏. 얘는 원래꺼가 바뀌어도 변형되지 않음

                if (snapshot.Count < 2)
                    return result;
                var orderedline = snapshot.OrderBy(x => x.rating).ToList();

                for (int i = 0; i+1 < orderedline.Count(); i += 2)
                {
                    Interlocked.Increment(ref _gameidcounter);
                    string conid1 = orderedline[i].conid;
                    string conid2 = orderedline[i + 1].conid;
                    int dbid1 = _dbiddata[conid1];
                    int dbid2 = _dbiddata[conid2];

                    if (ImmutableInterlocked.Update(ref _waitline_normgame, li => li.RemoveAll(val => val.conid == conid1))
                        && ImmutableInterlocked.Update(ref _waitline_normgame, li => li.RemoveAll(val => val.conid == conid2)))
                    {
                        result.Add((conid1, conid2, dbid1, dbid2));
                        ImmutableInterlocked.TryAdd(ref _roomdata_normgame, _gameidcounter, (new NormGame(), conid1, conid2));
                        _logger.LogInformation($"Game of ID: {_gameidcounter} Created between ID: {conid1}({dbid1}) & ID: {conid2}({dbid2})");
                    }
                }
            }
            return result;
        }

        //업데이트
        public void Update(float dt)
        {
            lock (removelistlock)
            {
                foreach (var gameid in _removelist)
                {
                    try
                    {
                        _logger.LogInformation($"GameID = {gameid} Disposed");
                        string cid1 = _roomdata_normgame[gameid].Player1ID, cid2 = _roomdata_normgame[gameid].Player2ID;
                        ImmutableInterlocked.TryRemove(ref _roomdata_normgame, gameid, out _);
                        unregister(cid1);
                        unregister(cid2);
                    }
                    finally
                    {
                        _removelist = new();
                    }
                }


                foreach (var (gameid, item) in _roomdata_train)
                {
                    item.Game.Update(dt); //얘는 플레이어가 나가면 바로 처리됨. 여기서 다룰 필요 없음
                }

                foreach (var (gameid, item) in _roomdata_normgame)
                {
                    item.Game.Update(dt);
                    if (item.Game.GetOutputs(1).Any(var => var is GameEndOutput))
                        _removelist.Add(gameid);
                }
            }
        }
    }
}