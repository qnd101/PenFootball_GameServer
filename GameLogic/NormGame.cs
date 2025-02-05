﻿using Microsoft.AspNetCore.Routing.Internal;
using PenFootball_GameServer.Services;
using System.Collections.Concurrent;
using System.Numerics;
using System.Collections.Immutable;

namespace PenFootball_GameServer.GameLogic
{
    public class KeyState
    {
        public Dictionary<GameKey, bool> KeyDict { get; private set; }
        public List<GameKey> KeysUp { get; private set; } = new List<GameKey>();
        public List<GameKey> KeysDown { get; private set; } = new List<GameKey>();
        
        public KeyState()
        {
            KeyDict = new Dictionary<GameKey, bool>();
            foreach (var key in new List<GameKey> { GameKey.Up, GameKey.Down, GameKey.Left, GameKey.Right })
                KeyDict.Add(key, false);
        }

        public void ResetKeyUpDown()
        {
            KeysDown.Clear();
            KeysUp.Clear();
        }

        public void Update(KeyEvent ke)
        {
            KeyDict[ke.Key] = (ke.EventType == KeyEventType.KeyDown) ? true : false;
            (ke.EventType == KeyEventType.KeyUp ? KeysUp : KeysDown).Add(ke.Key);
        }
    }

    public class Player
    {
        public Vector2 Pos { get; set; } = new Vector2();
        public Vector2 Vel { get; set; } = new Vector2();
        public KeyState KeyState { get; set; } = new KeyState();
        public bool IsOnGround { get; set; } = false;
        public int LeftJumps { get; set; } = 0;
        public (float lefttime, int pressedkey) DashData { get; set; } = (0, 9);
        public float LeftDashCoolTime { get; set; } = 0;

        protected const float eps = 0.001f;

        public void Reset(Vector2 spawn)
        { 
            Pos = spawn;
            Vel = Vector2.Zero;
            IsOnGround = false;
            LeftJumps = 0;
            DashData = (0, 0);
            LeftDashCoolTime = 0;
        }   

        public void Update(float dt, PhysConfig config)
        {
            var acc = 0;
            if (KeyState.KeyDict[GameKey.Left])
                acc -= 1;
            if (KeyState.KeyDict[GameKey.Right])
                acc += 1;

            Vel += Vector2.UnitX * acc * config.PlayerAcc * dt;

            Vel -= (config.PlayerDamp * Vel.X * dt) * Vector2.UnitX;

            Vel -= Vector2.UnitY * config.Gravity * dt;

            //땅에 있으면 player.LeftJump = 2

            if (LeftJumps > 0 && (
                (KeyState.KeyDict[GameKey.Up] && IsOnGround) || (KeyState.KeysDown.Contains(GameKey.Up) && Vel.Y < config.DoubleJmpVel)))
            {
                LeftJumps--;
                Vel = new Vector2(Vel.X, config.JumpVelY);
            }

            Pos += dt * Vel;

            if (Pos.Y - config.PlayerRadius < 0)
            {
                Pos = new Vector2(Pos.X, config.PlayerRadius + eps);
                LeftJumps = 2;
                IsOnGround = true;
                Vel = new Vector2(Vel.X, 0);
            }
            else
                IsOnGround = false;

            float minx = config.PlayerRadius, maxx = config.Width - config.PlayerRadius;

            if (Pos.X  < minx)
            {
                Pos = new Vector2(minx + eps, Pos.Y);
                Vel = new Vector2(0, Vel.Y);
            }

            if (Pos.X > maxx)
            {
                Pos = new Vector2(maxx - eps, Pos.Y);
                Vel = new Vector2(0, Vel.Y);
            }

            int dashdir = 0;

            var cntleft = KeyState.KeysDown.Count((elm) => (elm == GameKey.Left));
            var cntright = KeyState.KeysDown.Count((elm) => (elm == GameKey.Right));

            //대시가 성사될 수 있는 4가지 케이스
            if (DashData.pressedkey == 1 && cntright >= 1 && DashData.lefttime > 0)
            {
                dashdir = 1;
                DashData = (0, 0);
            }
            else if (DashData.pressedkey == -1 && cntleft >= 1 && DashData.lefttime > 0)
            {
                dashdir = -1;
                DashData = (0, 0);
            }
            else if (cntright >= 2)
            {
                dashdir = 1;
                DashData = (0, 0);
            }
            else if (cntleft >= 2)
            {
                dashdir = -1;
                DashData = (0, 0);
            }
            //대시가 성사되지 않았지만 키를 눌렀을 경우 초기화
            else if (cntright == 1)
            {
                DashData = (config.DashTimeout, 1);
            }
            else if (cntleft == 1)
            {
                DashData = (config.DashTimeout, -1);
            }
            //대시가 성사되지 않았고 키도 누르지 않았음
            else
            {
                DashData = (DashData.lefttime - dt, DashData.pressedkey);
            }

            LeftDashCoolTime -= dt;
            //대시가 성사되었더라도 쿨타임이 차야하며 현재 Y방향 속도가 너무 크면 안됨
            if (dashdir != 0 && LeftDashCoolTime <= 0 && Vel.Y < config.DoubleJmpVel)
            {
                Vel = new Vector2(dashdir * config.DashVelX, Vel.Y + config.DashVelY);
                LeftDashCoolTime = config.DashCooltime;
            }
        }
    }

    public class Ball
    {
        public Vector2 Pos { get; private set; } = new Vector2();
        public Vector2 Vel { get; private set; } = new Vector2();

        private const float eps = 0.001f;

        public void Reset(Vector2 spawn)
        {
            Pos = spawn;
            Vel = Vector2.Zero;
        }

        public void Update(float dt, IEnumerable<Player> players, PhysConfig config)
        {
            Vel -= config.Gravity * dt * Vector2.UnitY;
            Vel -= Vel.X * dt * config.BallDamp * Vector2.UnitX;

            var collist = players.Select(pl => (pl, (pl.Pos - Pos).Length() < config.BallRadius + config.PlayerRadius));
            var colcnt = collist.Count(x => x.Item2);
            if (colcnt>=2)
            {
                Vel = new Vector2(0, config.BallCollideBounceVel);
            }
            else if (colcnt == 1)
            {
                var player = collist.Where(item => item.Item2).First().Item1;
                var dispx = Pos.X - player.Pos.X;
                var newvelX = (player.Vel.X + Math.Sign(dispx) * config.KickVel) * Math.Abs(dispx) / (config.PlayerRadius + config.BallRadius) * config.PlayerBallCoupling; //수정해야
                var newvelY = player.Vel.Y + config.PlayerBallBounceY;
                Vel = new Vector2(newvelX, newvelY);
            }

            Pos += Vel * dt;

            if (Pos.Y - config.BallRadius < 0)
            {
                Pos = new Vector2(Pos.X, eps + config.BallRadius);
                Vel = new Vector2(Vel.X, -Vel.Y * config.BounceCoeff);
            }

            if (Pos.X - config.BallRadius < 0)
            {
                Pos = new Vector2(config.BallRadius + eps, Pos.Y);
                Vel = new Vector2(-config.BounceCoeff * Vel.X,  Vel.Y);
            }

            if (Pos.X + config.BallRadius > config.Width)
            {
                Pos = new Vector2(config.Width - config.BallRadius - eps, Pos.Y);
                Vel = new Vector2(-config.BounceCoeff * Vel.X, Vel.Y);
            }

            if ((Pos.X < config.GoalWidth + config.BallRadius / 2 || Pos.X > config.Width - config.GoalWidth - config.BallRadius / 2)
                && Pos.Y < config.GoalHeight + config.BallRadius
                && Pos.Y - dt * Vel.Y > config.GoalHeight + config.BallRadius)
            {
                var dirfall = Pos.X > config.Width / 2 ? -1 : 1;
                Pos = new Vector2(Pos.X, config.GoalHeight + config.BallRadius + eps);
                Vel = new Vector2(Math.Abs(Vel.X) < 1 ? dirfall * 3 : Vel.X, -Vel.Y * config.BounceCoeff);
            }
        }
    }

    //config data about the game
    public class NormGameConfig
    {
        public float PreviewTime { get; set; } = 3; //게임 시작까지 걸리는 시간
        public int MaxScore { get; set; } = 10;
        public Vector2 Spawn1 { get; set; } = new Vector2(30, 20);
        public Vector2 Spawn2 { get; set; } = new Vector2(430, 20);
        public Vector2 BallSpawn { get; set; } = new Vector2(230, 150);
    }

    public class NormGame
    {
        public ConcurrentQueue<IGameEvent> EventQueue { get; } = new ConcurrentQueue<IGameEvent>();
        public Player Player1 { get; private set; } = new Player();
        public Player Player2 { get; private set; } = new Player();

        public Ball BallObj { get; } = new Ball();

        public PhysConfig PhysConfig { get; }
        public NormGameConfig GameConfig { get; } = new NormGameConfig();   

        public int Score1 { get; private set; } = 0;
        public int Score2 { get; private set; } = 0;    

        public Player[] Players { get; }
        public ConcurrentQueue<IGameOutput> OutputQueue { get; private set; } = new ConcurrentQueue<IGameOutput>();

        private float timecounter = 0;

        public NormGame(float previewtime)
        {
            this.GameConfig.PreviewTime = previewtime;  
            Players = new Player[2] { Player1, Player2 };
            PhysConfig = PhysConfig.ClassicConfig();
            TimeDimAttribute.Rescale(PhysConfig, 0.027f);
            ResetRound();
            OutputQueue.Enqueue(new PreviewOutput(this.PhysConfig.GetConfigJson()));
        }

        private void ResetRound()
        {
            Player1.Reset(GameConfig.Spawn1);
            Player2.Reset(GameConfig.Spawn2);
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

        public void Update(float dt)
        {
            if(timecounter < this.GameConfig.PreviewTime)
            {
                timecounter += dt;
                return;
            }
            Player1.KeyState.ResetKeyUpDown();
            Player2.KeyState.ResetKeyUpDown();

            while (EventQueue.TryDequeue(out var item))
            {
                if (item is KeyEvent ke)
                {
                    if (ke.WhichPlayer == 2)
                    {
                        ke = new KeyEvent(ke.WhichPlayer, flipKey(ke.Key), ke.EventType);
                    }
                    (ke.WhichPlayer == 1 ? Player1 : Player2).KeyState.Update(ke);
                }
                else if (item is ExitEvent ee)
                {
                    OutputQueue.Enqueue(new GameEndOutput($"*{ee.WhichPlayer}* left the game.", 3-ee.WhichPlayer));
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
                if(!ChkGameEnd())
                    ResetRound();
            }
            else if (BallObj.Pos.X > PhysConfig.Width - PhysConfig.GoalWidth + PhysConfig.BallRadius && BallObj.Pos.Y < PhysConfig.GoalHeight+PhysConfig.BallRadius)
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
                bool wasfierce = Score2 == GameConfig.MaxScore - 1;
                if (wasfierce)
                    addstr = " after a fierce battle";
                OutputQueue.Enqueue(new GameEndOutput($"*1* reached {GameConfig.MaxScore} points{addstr}.", 1, wasfierce));
                return true;
            }
            if (Score2 >= GameConfig.MaxScore)
            {
                var addstr = "";
                bool wasfierce = Score1 == GameConfig.MaxScore - 1;
                if (wasfierce)
                    addstr = " after a fierce battle";
                OutputQueue.Enqueue(new GameEndOutput($"*2* reached {GameConfig.MaxScore} points{addstr}.", 2, wasfierce));
                return true;
            }
            return false;
        }

        public Frame GetFrame(int who)
        {
            var fr = new Frame(VectorForJSON.FromVec2(Player1.Pos), VectorForJSON.FromVec2(Player2.Pos), VectorForJSON.FromVec2(BallObj.Pos));
            return who == 1 ? fr : Frame.Flip(fr, PhysConfig.Width);
        }
        public IEnumerable<IGameOutput> GetOutputs(int who)
        {
            return who == 1? this.OutputQueue : this.OutputQueue.Select(x => x.Flip());
        }

        public void FlushOutputs()
        {
            this.OutputQueue.Clear();
        }
    }
}
