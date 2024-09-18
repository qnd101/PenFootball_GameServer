using Microsoft.Extensions.Primitives;
using PenFootball_GameServer.GameLogic;

namespace PenFootball_GameServer.Services
{
    public interface IGameOutput
    {
        public IGameOutput Flip();
    }


    //이 타입들은 모든 종류의 게임들에서 공통적으로 이용할 것 (IGameEvent도 마찬가지)
    //따라서 각 게임에 대해서는 다소의 redundancy가 있어도 됨.

    public class PreviewOutput : IGameOutput
    {
        public GeoConfigJson Config { get; set; } //Config에 포함된 기하적 데이터는 게임 시작 시에 딱 한번만 보내면 됨

        public PreviewOutput(GeoConfigJson configJson)
        {
            Config = configJson;
        }

        public IGameOutput Flip() => this;
    }

    //나중에 여러 통계 자료도 추가할 것
    public class GameEndOutput : IGameOutput
    {
        public string Summary { get; set; } //승리 방법. 
        //규약: *1* 혹은 *2* 와 같이 적으면 클라이언트 측에서 이름으로 교체해줌 (Game은 플레이어들의 실제 이름을 모름!)
        public int Winner { get; set; } //이긴 사람의 번호 (ex) 1번/2번 플레이어. 2번 플레이어에게 보낼 때는 flip해줘야 함

        public GameEndOutput(string summary, int winner)
        {
            Summary = summary;
            Winner = winner;
        }

        public IGameOutput Flip()
        {
            return new GameEndOutput(Summary.Replace("*1*", "*t*").Replace("*2*", "*1*").Replace("*t*", "*2*"), 3 - Winner);
        }
    }

    public class ScoreOutput : IGameOutput
    {
        public int Score1 { get; set; }
        public int Score2 { get; set; }

        public ScoreOutput(int score1, int score2)
        {
            Score1 = score1;
            Score2 = score2;
        }

        public IGameOutput Flip() => new ScoreOutput(Score2, Score1);
    }

    public class ChatOutput : IGameOutput
    {
        public int Who { get; set; } //누가 채팅을 보냈는지 2번 플레이어에게 보낼 떄는 flip해줘야 함
        public string Message { get; set; } //메세지 내용

        public ChatOutput(int who, string message)
        {
            Who = who;
            Message = message;
        }

        public IGameOutput Flip() => new ChatOutput(3 - Who, Message);
    }

    public class GameFoundOutput : IGameOutput
    {
        public GameType WhichGame { get; set; }
        public int[] IDs { get; set; }

        public GameFoundOutput(GameType whichgame,  int[] ids) { 
            WhichGame = whichgame;
            IDs = ids;
        }
        private static int[] flipoddeven(int[] basearr)
        {
            var result = new int[basearr.Length];
            for (int i = 0; i < basearr.Length; i += 2)
            {
                result[i] = basearr[i + 1];
                result[i+1] = basearr[i];
            }
            return result;
        }
        public IGameOutput Flip() => new GameFoundOutput(WhichGame, flipoddeven(IDs));
    }

    public class WaitingInfoOutput : IGameOutput
    {
        public GameType WhichGame { get; set; }
        public int WaitCount { get; set; }

        public WaitingInfoOutput(GameType whichgame, int waitcount) { WhichGame = whichgame; WaitCount = waitcount; }

        public IGameOutput Flip() => this;
    }
}
