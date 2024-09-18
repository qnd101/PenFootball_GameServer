using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using PenFootball_GameServer.Services;

namespace PenFootball_GameServer.GameLogic
{
    public class TwoVTwoGameConfig
    {
        public float PreviewTime { get; set; } = 3; //게임 시작까지 걸리는 시간
        public int MaxScore { get; set; } = 10;
        //순서대로 1,2,3,4
        public Vector2 Atk1Spawn { get; set; } 
        public Vector2 Atk2Spawn { get; set; }
        public Vector2 Def1Spawn { get; set; }
        public Vector2 Def2Spawn { get; set; }
        public Vector2 BallSpawn { get; set; }
    }

    public class ConfinedPlayer : Player
    {
        public float MinX { get; set; }
        public float MaxX { get; set; }

        public ConfinedPlayer(float minX, float maxX)
        {
            MinX = minX;
            MaxX = maxX;
        }   

        public void Update(float dt, PhysConfig config)
        {
            base.Update(dt, config);
            if (Pos.X < MinX)
            {
                Pos = new Vector2(MinX + eps, Pos.Y);
                Vel = new Vector2(0, Vel.Y);
            }

            if (Pos.X > MaxX)
            {
                Pos = new Vector2(MaxX - eps, Pos.Y);
                Vel = new Vector2(0, Vel.Y);
            }
        }
    }
    public class TwoVTwoGame
    {
        public ConcurrentQueue<IGameEvent> EventQueue { get; } = new ConcurrentQueue<IGameEvent>();
        public ConfinedPlayer Atk1 { get; private set; }
        public ConfinedPlayer Atk2 { get; private set; }
        public ConfinedPlayer Def1 { get; private set; }
        public ConfinedPlayer Def2 { get; private set; }

        public Ball BallObj { get; } = new Ball();

        public PhysConfig PhysConfig { get; }
        public TwoVTwoGameConfig GameConfig { get; }

        public int Score1 { get; private set; } = 0;
        public int Score2 { get; private set; } = 0;

        private List<int> leftplayers = new List<int> { 1, 2, 3, 4 };

        public ConfinedPlayer[] Players { get; }
        public ConcurrentQueue<IGameOutput> OutputQueue { get; private set; } = new ConcurrentQueue<IGameOutput>();

        public TwoVTwoGame()
        {
            PhysConfig = PhysConfig.ClassicConfig();
            TimeDimAttribute.Rescale(PhysConfig, 0.027f * 0.9f);
            PhysConfig.Width = 800;
            PhysConfig.GoalHeight = 130;
            var def1spawn = 60;
            GameConfig = new TwoVTwoGameConfig()
            {
                Def1Spawn = new Vector2(def1spawn, 30),
                Def2Spawn = new Vector2(PhysConfig.Width - def1spawn, 30),
                Atk2Spawn = new Vector2(PhysConfig.Width/2-def1spawn, 30),
                Atk1Spawn = new Vector2(PhysConfig.Width/2+def1spawn, 30),
                BallSpawn = new Vector2(PhysConfig.Width/2, 150)
            };

            Atk1 = new ConfinedPlayer(PhysConfig.Width / 2, PhysConfig.Width);
            Atk2 = new ConfinedPlayer(0, PhysConfig.Width/2);
            Def2 = new ConfinedPlayer(PhysConfig.Width / 2, PhysConfig.Width);
            Def1 = new ConfinedPlayer(0, PhysConfig.Width / 2);
            Players = new ConfinedPlayer[4] { Atk1, Atk2, Def1, Def2 };

            ResetRound();
            OutputQueue.Enqueue(new PreviewOutput(this.PhysConfig.GetConfigJson()));
        }

        private void ResetRound()
        {
            Atk1.Reset(GameConfig.Atk1Spawn);
            Atk2.Reset(GameConfig.Atk2Spawn);
            Def1.Reset(GameConfig.Def1Spawn);
            Def2.Reset(GameConfig.Def2Spawn);
            BallObj.Reset(GameConfig.BallSpawn);
        }

        private GameKey flipKey(GameKey key)
        {
            switch (key)
            {
                case GameKey.Left: return GameKey.Right;
                case GameKey.Right: return GameKey.Left;
                default: return key;
            }
        }

        private int chpersp(int whichPlayer) => whichPlayer > 2 ? whichPlayer - 2 : whichPlayer + 2;
        private int getside(int whichPlayer) => whichPlayer % 2 == 0 ? 2 : 1;

        public void Update(float dt)
        {
            foreach(Player pl in Players)
                pl.KeyState.ResetKeyUpDown();

            while (EventQueue.TryDequeue(out var item))
            {
                if (item is KeyEvent ke)
                {
                    if (ke.WhichPlayer % 2 == 0)
                    {
                        ke = new KeyEvent(ke.WhichPlayer, flipKey(ke.Key), ke.EventType);
                    }
                    Players[ke.WhichPlayer-1].KeyState.Update(ke);
                }
                else if (item is ExitEvent ee)
                {
                    var side = getside(ee.WhichPlayer);
                    OutputQueue.Enqueue(new GameEndOutput($"Someone in team *{side}* left", 3 - side));
                    //leftplayers.Remove(ee.WhichPlayer);
                    //for (int side = 0; side < 2; side++)
                    //{
                    //    if (!leftplayers.Any(item => getside(item) == side))
                    //        OutputQueue.Enqueue(new GameEndOutput($"All players in side *{side}* left the game", 3-side));
                    //}
                    //var leftplayer = Players[(getside(ee.WhichPlayer) == 1 ? 4 - ee.WhichPlayer : 6 - ee.WhichPlayer) - 1];
                    //leftplayer.MinX = 0;
                    //leftplayer.MaxX = PhysConfig.Width; //전체 공간을 쥐어줌
                }
            }

            foreach (var player in Players)
            {
                player.Update(dt, PhysConfig);
            }
            BallObj.Update(dt, Players, PhysConfig);

            if (BallObj.Pos.X < PhysConfig.GoalWidth - PhysConfig.BallRadius && BallObj.Pos.Y < PhysConfig.GoalHeight + PhysConfig.BallRadius)
            {
                Score2 += 1;
                OutputQueue.Enqueue(new ScoreOutput(Score1, Score2));
                if (!ChkGameEnd())
                    ResetRound();
            }
            else if (BallObj.Pos.X > PhysConfig.Width - PhysConfig.GoalWidth + PhysConfig.BallRadius && BallObj.Pos.Y < PhysConfig.GoalHeight + PhysConfig.BallRadius)
            {
                Score1 += 1;
                OutputQueue.Enqueue(new ScoreOutput(Score1, Score2));
                if (!ChkGameEnd())
                    ResetRound();
            }
        }
        private bool ChkGameEnd()
        {
            if (Score1 >= GameConfig.MaxScore)
            {
                var addstr = "";
                if (Score2 == GameConfig.MaxScore - 1)
                    addstr = " after a fierce battle";
                OutputQueue.Enqueue(new GameEndOutput($"*1* reached {GameConfig.MaxScore} points{addstr}.", 1));
                return true;
            }
            if (Score2 >= GameConfig.MaxScore)
            {
                var addstr = "";
                if (Score1 == GameConfig.MaxScore - 1)
                    addstr = " after a fierce battle";
                OutputQueue.Enqueue(new GameEndOutput($"*2* reached {GameConfig.MaxScore} points{addstr}.", 2));
                return true;
            }
            return false;
        }
        
        public Frame4 GetFrame(int side)
        {
            var fr = new Frame4(VectorForJSON.FromVec2(Atk1.Pos)
                , VectorForJSON.FromVec2(Atk2.Pos)
                , VectorForJSON.FromVec2(Def1.Pos)
                , VectorForJSON.FromVec2(Def2.Pos)
                , VectorForJSON.FromVec2(BallObj.Pos));
            return side ==  1 ? fr : Frame4.Flip(fr, PhysConfig.Width);
        }
        public IEnumerable<IGameOutput> GetOutputs(int side)
        {
            return side == 1 ? this.OutputQueue : this.OutputQueue.Select(x => x.Flip());
        }

        public void FlushOutputs()
        {
            this.OutputQueue.Clear();
        }
    }
}
