namespace PenFootball_GameServer.Services
{
    //역할: 게임 종류를 지칭
    //지금 상황으로는 PlayerState에도 일대일 대응되는데 (None <-> Waiting) 앞으로도 그럴지는 미지수
    public enum GameType
    {
        None,
        Training,
        NormGame,
        TwoVTwoGame
    }

    //플레이어의 상태를 다루기 위한 타입들
    //게임의 종류를 지니고 있어야. 게임 ID도 넣을지는 고민중
    public interface IPlayerState 
    {
        GameType GameType { get; }
    }

    //대기중
    public class WaitingState : IPlayerState
    {
        public GameType GameType { get => GameType.None; }
        public GameType WaitingFor { get; set; }

        public WaitingState(GameType waitingfor) { WaitingFor = waitingfor; }
    }

    //훈련 중
    public class TrainingState : IPlayerState
    {
        public GameType GameType { get => GameType.Training; }
        public int GameID { get; set; }

        public TrainingState(int gameID)
        {
            GameID = gameID;
        } 
    }

    //노멀 모드로 게임중 (랭킹전)
    public class NormGameState : IPlayerState
    {
        public GameType GameType { get => GameType.NormGame; }
        //상대의 ID (당연히 ConID)
        public string OppID { get; set; }
        //플레이어 종류 (1번 혹은 2번)
        public int PlayerType { get; set; }
        //현재 게임 ID
        public int GameID { get; set; }

        public NormGameState(string oppid, int playertype, int gameid)
        {
            OppID = oppid;
            PlayerType = playertype;
            GameID = gameid;
        }
    }

    public class TwoVTwoGameState : IPlayerState
    {
        public GameType GameType { get => GameType.TwoVTwoGame; }
        //1,2,3,4
        public int PlayerType { get; set; } 
        //1, 2
        public int SideType { get; set; }
        public string[] PlayerConIds { get; set; }

        public int GameID { get; set; }

        public TwoVTwoGameState(int playerType, string[] playerConIds, int gameID)
        {
            PlayerType = playerType;
            SideType = playerType % 2 == 0 ? 2 : 1;
            PlayerConIds = playerConIds;
            GameID = gameID;
        }
    }
}
