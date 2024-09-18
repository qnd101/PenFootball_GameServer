using System.Collections.Concurrent;
using PenFootball_GameServer.Services;
using System.Numerics;

namespace PenFootball_GameServer.GameLogic
{
    public class TrainGameConfig
    {
        public Vector2 Spawn { get; set; } = new Vector2(30, 20);
        public Vector2 BallSpawn { get; set; } = new Vector2(230, 150);
    }

    public class TrainGame
    {
        public ConcurrentQueue<IGameEvent> EventQueue { get; } = new ConcurrentQueue<IGameEvent>();
        public Player Player { get; private set; } = new Player();

        public Ball BallObj { get; } = new Ball();

        public PhysConfig PhysConfig { get; }
        public TrainGameConfig GameConfig { get; } = new TrainGameConfig();

        public bool GivePreview { get; }

        private bool _outputflushed = false;

        public TrainGame()
        {
            PhysConfig = PhysConfig.ClassicConfig();
            TimeDimAttribute.Rescale(PhysConfig, 0.027f);
            ResetRound();
        }
        private void ResetRound()
        {
            Player.Reset(GameConfig.Spawn);
            BallObj.Reset(GameConfig.BallSpawn);
        }

        public void Update(float dt)
        {
            Player.KeyState.ResetKeyUpDown();

            while (EventQueue.TryDequeue(out var item))
            {
                if (item is KeyEvent ke)
                {
                    Player.KeyState.Update(ke);
                }
            }

            Player.Update(dt, PhysConfig);
            BallObj.Update(dt, new List<Player> { Player }, PhysConfig);

            if (BallObj.Pos.X < PhysConfig.GoalWidth - PhysConfig.BallRadius && BallObj.Pos.Y < PhysConfig.GoalHeight + PhysConfig.BallRadius)
            {
                ResetRound();
            }
            else if (BallObj.Pos.X > PhysConfig.Width - PhysConfig.GoalWidth + PhysConfig.BallRadius && BallObj.Pos.Y < PhysConfig.GoalHeight + PhysConfig.BallRadius)
            {
                ResetRound();
            }
        }

        public Frame GetFrame()
        {
            return new Frame(VectorForJSON.FromVec2(Player.Pos), new VectorForJSON(-100, 0), VectorForJSON.FromVec2(BallObj.Pos));
        }
        public IEnumerable<IGameOutput> GetOutputs() => _outputflushed ? new List<IGameOutput>() : new List<IGameOutput> { new PreviewOutput(PhysConfig.GetConfigJson()) };

        public void FlushOutputs()
        {
            _outputflushed = true;
        }
    }

}
