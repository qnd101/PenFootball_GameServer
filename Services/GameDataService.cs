using PenFootball_GameServer.GameLogic;
using PenFootball_GameServer.Hubs;
using System.Collections.Immutable;
using System.ComponentModel.Design.Serialization;

namespace PenFootball_GameServer.Services
{

    //나중에 각종 통계도 추가할 것
    public record GameResultData(int Player1ID, int Player2ID, int winner, bool wasdeuce, int score1, int score2);

    public interface IGameDataService
    {
        //ConID와 DBID 사이 왔다갔다
        string? GetConID(int id);
        int? GetDBID(string conid);
        IEnumerable<string> AllConIDs();
        IEnumerable<int> AllGameIDs();

        //플레이어 상태 가져오기 / 게임 상태 가져오기
        IPlayerState? GetPlayerState(string conid);

        //입력
        void KeyInput(string conid, GameKey keytype, KeyEventType eventtype);
        void ExitInput(string conid); 

        //플레이어 추가/제거 (Double connection이면 아무 작업도 안하도록)
        void EnterTrain(string conid, int dbid); 
        void EnterNormGame (string conid, int dbid, int rating);
        void EnterTwoVTwo (string conid, int dbid, int rating);

        //출력
        object? GetFrame(string conid);
        IEnumerable<IGameOutput> GetOutputs (string conid); //단발성 아웃풋
        void FlushOutputs(int gameid);
        IEnumerable<GameResultData> GetGameResults(); //Post를 위한 데이터

        //업데이트
        //매치 메이킹 -> 생성된 ID 쌍들을 반환
        void MakeMatches(float dt);
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
        private ImmutableDictionary<int, (TwoVTwoGame Game, string[] PlayerIDs)> _roomdata_twovtwo;

        private ImmutableList<(string conid, int rating)> _waitline_normgame;
        private ImmutableList<(string conid, int rating)> _waitline_twovtwo;

        //Data of Currently Playing/Waiting players
        private ImmutableDictionary<string, int> _dbiddata;

        private ILogger<GameDataService> _logger;

        private int _gameidcounter = 0;
        private float _normgamematchcounter = 10;
        private static float normgametimeout = 10;
        List<int> _removelistnorm = new();
        List<int> _removelisttvt = new();

        List<int> _madematches = new();

        private float _gamepreviewtime = 2;

        private float _waitoutputconter = 0.5f;
        private static float _waitoutputtimeout = 0.5f;

        public GameDataService(ILogger<GameDataService> logger)
        {
            _logger = logger;
            _roomdata_normgame = ImmutableDictionary<int, (NormGame, string, string)>.Empty;
            _roomdata_train = ImmutableDictionary<int, (TrainGame, string)>.Empty;
            _roomdata_twovtwo = ImmutableDictionary<int, (TwoVTwoGame, string[])>.Empty;

            _waitline_normgame = ImmutableList<(string, int)>.Empty;
            _waitline_twovtwo = ImmutableList<(string, int)>.Empty;
            _dbiddata = ImmutableDictionary<string, int>.Empty;
        }

        public string? GetConID(int id)
        {
            var items = _dbiddata.Where(item => item.Value == id);
            if (items.Any())
                return items.First().Key;
            return null;
        }

        public int? GetDBID(string conid)
        {
            if (_dbiddata.Keys.Contains(conid))
                return _dbiddata[conid];
            return null;
        }

        public IEnumerable<string> AllConIDs() => _dbiddata.Keys;
        public IEnumerable<int> AllGameIDs() => _roomdata_normgame.Keys.Concat(_roomdata_train.Keys).Concat(_roomdata_twovtwo.Keys);

        public IPlayerState? GetPlayerState(string conid)
        {
            if (_waitline_normgame.Any(x => x.conid == conid))
                return new WaitingState(GameType.NormGame);
            else if (_waitline_twovtwo.Any(x => x.conid == conid))
                return new WaitingState(GameType.TwoVTwoGame);

            foreach (var item in _roomdata_train)
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

            foreach (var item in _roomdata_twovtwo)
            {
                var whichplayer = Array.FindIndex<string>(item.Value.PlayerIDs, (x => x == conid)) + 1;
                if (whichplayer > 0)
                {
                    return new TwoVTwoGameState(whichplayer, item.Value.PlayerIDs, item.Key);
                }

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
                case TwoVTwoGameState tvts:
                    var ev3 = new KeyEvent(tvts.PlayerType, keytype, eventtype);
                    _roomdata_twovtwo[tvts.GameID].Game.EventQueue.Enqueue(ev3);
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
            ExitInput(conid);
            Interlocked.Increment(ref _gameidcounter);
            ImmutableInterlocked.TryAdd(ref _roomdata_train, _gameidcounter, (new TrainGame(), conid));
            _logger.LogInformation($"Train Game of ID: {_gameidcounter} Created by ID: {conid}({playerid})");
        }

        public void EnterNormGame(string conid, int dbid, int rating)
        {
            if (!register(conid, dbid))
                return;
            ExitInput(conid);
            ImmutableInterlocked.Update(ref _waitline_normgame, (_waitline) => _waitline.Add((conid, rating)));
            _logger.LogInformation($"Player of ID: {conid}({dbid}) waiting for norgame");
        }

        public void EnterTwoVTwo(string conid, int dbid, int rating)
        {
            if (!register(conid, dbid))
                return;
            ExitInput(conid);

            ImmutableInterlocked.Update(ref _waitline_twovtwo, (_waitline) => _waitline.Add((conid, rating)));
            _logger.LogInformation($"Player of ID: {conid}({dbid}) waiting for 2v2 ({_waitline_twovtwo.Count}/4)");
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
            switch (GetPlayerState(conid))
            {
                case WaitingState ws:
                    if(ws.WaitingFor==GameType.NormGame)
                        ImmutableInterlocked.Update(ref _waitline_normgame, (li) => li.RemoveAll(x => x.conid == conid));
                    else if (ws.WaitingFor==GameType.TwoVTwoGame)
                        ImmutableInterlocked.Update(ref _waitline_twovtwo, (li) => li.RemoveAll(x => x.conid == conid));
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
                case TwoVTwoGameState tvts:
                    _roomdata_twovtwo[tvts.GameID].Game.EventQueue.Enqueue(new ExitEvent(tvts.PlayerType));
                    break;
            }
        }

        //conid에게 보이는 시야를 가져옴
        public object? GetFrame(string conid)
        {
            switch (GetPlayerState(conid))
            {
                case TrainingState ts:
                    return _roomdata_train[ts.GameID].Game.GetFrame();
                case NormGameState ns:
                    return _roomdata_normgame[ns.GameID].Game.GetFrame(ns.PlayerType);
                case TwoVTwoGameState tvts:
                    return _roomdata_twovtwo[tvts.GameID].Game.GetFrame(tvts.SideType);
                default:
                    return null;
            }
        }
        public IEnumerable<IGameOutput> GetOutputs(string conid)
        {
            switch (GetPlayerState(conid))
            {
                case WaitingState ws:
                    if (_waitoutputconter < 0)
                    {
                        if (ws.WaitingFor==GameType.NormGame)
                            return new List<IGameOutput> { new WaitingInfoOutput(GameType.NormGame, _waitline_normgame.Count) };
                        if (ws.WaitingFor==GameType.TwoVTwoGame)
                            return new List<IGameOutput> { new WaitingInfoOutput(GameType.TwoVTwoGame, _waitline_twovtwo.Count) };
                    }
                    return Enumerable.Empty<IGameOutput>();
                case NormGameState ns:
                    var mygame = _roomdata_normgame[ns.GameID];
                    var temp = mygame.Game.GetOutputs(ns.PlayerType);
                    if (_madematches.Contains(ns.GameID))
                    {
                        IGameOutput output = new GameFoundOutput(GameType.NormGame, new int[] { _dbiddata[mygame.Player1ID], _dbiddata[mygame.Player2ID] });
                        if (ns.PlayerType == 2)
                            output = output.Flip();
                        return temp.Append(output);
                    }
                    return temp;
                case TrainingState ts:
                    return _roomdata_train[ts.GameID].Game.GetOutputs();
                case TwoVTwoGameState tvts:
                    var mygame2 = _roomdata_twovtwo[tvts.GameID];
                    var temp2 = mygame2.Game.GetOutputs(tvts.SideType);
                    if (_madematches.Contains(tvts.GameID))
                    {
                        IGameOutput output = new GameFoundOutput(GameType.TwoVTwoGame, tvts.PlayerConIds.Select(item => _dbiddata[item]).ToArray());
                        if(tvts.SideType == 2)
                            output = output.Flip();
                        return temp2.Append(output);
                    }
                    return temp2;
                default:
                    return Enumerable.Empty<IGameOutput>();
            }
        }

        //모든 끝난 게임들의 결과를 가져옴 (랭킹에 반영되는)
        public IEnumerable<GameResultData> GetGameResults()
        {
            List<GameResultData> results = new();
            foreach (var item in _roomdata_normgame.Values)
            {
                if (item.Game.GetOutputs(1).FirstOrDefault(x => x is GameEndOutput) is GameEndOutput go)
                    results.Add(new GameResultData(_dbiddata[item.Player1ID], _dbiddata[item.Player2ID], go.Winner, go.WasDeuce, item.Game.Score1, item.Game.Score2));
            }
            return results;
        }

        public void FlushOutputs(int gameid)
        {
            if (_roomdata_normgame.Keys.Contains(gameid))
            {
                _roomdata_normgame[gameid].Game.FlushOutputs();
            }
            else if (_roomdata_train.Keys.Contains(gameid))
            {
                _roomdata_train[gameid].Game.FlushOutputs();
            }
            else if (_roomdata_twovtwo.Keys.Contains(gameid))
            {
                _roomdata_twovtwo[gameid].Game.FlushOutputs();
            }
            _madematches = new List<int>();
        }

        private static void Swap<T>(T[] array, int index1, int index2)
        {
            var temp = array[index1];
            array[index1] = array[index2];
            array[index2] = temp;
        }

        public void MakeMatches(float dt)
        {
            //match 2v2 
            if(_waitline_twovtwo.Count >= 4)
            {
                var interest = _waitline_twovtwo.Take(4).OrderBy(item=>item.rating);
                var conids = interest.Select(item => item.conid).ToArray();
                //제일 높은 놈과 낮은 놈이 한팀
                Swap(conids, 2, 3); 
                var random = (new Random()).Next(4);

                //랜덤하게 역할을 할당
                if (random > 2)
                    Swap(conids, 0, 2);
                if (random % 2 == 0)
                    Swap(conids, 1, 3);

                Interlocked.Increment(ref _gameidcounter);
                var newgame = new TwoVTwoGame();
                ImmutableInterlocked.TryAdd(ref _roomdata_twovtwo, _gameidcounter, (newgame, conids));
                ImmutableInterlocked.Update(ref _waitline_twovtwo, line => line.RemoveAll(item => conids.Contains(item.conid)));
                _madematches.Add(_gameidcounter);
            }

            if (_normgamematchcounter < 0)
            {
                _normgamematchcounter = normgametimeout;
                var snapshot = _waitline_normgame; //현재 스냅숏. 얘는 원래꺼가 바뀌어도 변형되지 않음

                if (snapshot.Count < 2)
                    return;
                var orderedline = snapshot.OrderBy(x => x.rating).ToList();

                var skipindex = -1;
                //홀수명이면 랜덤하게 한명 제외
                if (orderedline.Count > 1 && orderedline.Count %2 ==1) 
                    skipindex = (new Random()).Next(orderedline.Count);

                for (int i = 0; i+1 < orderedline.Count(); i += 2)
                {
                    Interlocked.Increment(ref _gameidcounter);
                    if (i == skipindex)
                        i++;
                    string conid1 = orderedline[i].conid;
                    if (i+1 == skipindex)
                        i++;
                    string conid2 = orderedline[i + 1].conid;
                    int dbid1 = _dbiddata[conid1];
                    int dbid2 = _dbiddata[conid2];

                    if (ImmutableInterlocked.Update(ref _waitline_normgame, li => li.RemoveAll(val => val.conid == conid1))
                        && ImmutableInterlocked.Update(ref _waitline_normgame, li => li.RemoveAll(val => val.conid == conid2)))
                    {
                        _madematches.Add(_gameidcounter);
                        ImmutableInterlocked.TryAdd(ref _roomdata_normgame, _gameidcounter, (new NormGame(_gamepreviewtime), conid1, conid2));
                        _logger.LogInformation($"Game of ID: {_gameidcounter} Created between ID: {conid1}({dbid1}) & ID: {conid2}({dbid2})");
                    }
                }
            }
        }

        //업데이트
        public void Update(float dt)
        {
            if (_waitoutputconter < 0)
                _waitoutputconter = _waitoutputtimeout;
            _normgamematchcounter -= dt;
            _waitoutputconter -= dt;

            foreach (var gameid in _removelistnorm)
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
                    _removelistnorm = new();
                }
            }

            foreach (var gameid in _removelisttvt)
            {
                try
                {
                    _logger.LogInformation($"GameID = {gameid} Disposed");
                    string[] cids = _roomdata_twovtwo[gameid].PlayerIDs;
                    ImmutableInterlocked.TryRemove(ref _roomdata_twovtwo, gameid, out _);
                    foreach (var cid in cids) 
                        unregister(cid);
                }
                finally
                {
                    _removelisttvt = new();
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
                    _removelistnorm.Add(gameid);
            }
            foreach(var (gameid, item) in _roomdata_twovtwo)
            {
                item.Game.Update(dt);
                if (item.Game.GetOutputs(1).Any(var => var is GameEndOutput))
                    _removelisttvt.Add(gameid);
            }
        }
    }
}