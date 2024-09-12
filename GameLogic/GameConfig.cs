using System.Numerics;
using System.Security.Cryptography;

namespace PenFootball_GameServer.GameLogic
{
    public class DimensionAttribute : Attribute
    {
        public float Length { get; set; }
        public float Time { get; set; }
        public bool IsY { get; set; }
        public DimensionAttribute(float length, float time, bool isY =false) {
            Length = length;
            Time = time;
            IsY = isY;
        }
    }

    public record GameConfigJson(float width, float goalWidth, float goalHeight, float playerRadius, float ballRadius, float previewTime);

    public class GameConfig
    {
        public float previewTime { get; set; } = 3; //게임 시작까지 걸리는 시간
        public int MaxScore { get; set; } = 10;
        [Dimension(1, -2,true)]
        public float Gravity { get; set; } = 130;
        [Dimension(1, -2)]
        public float PlayerAcc { get; set; } = 120;
        [Dimension(0,-1)]
        public float PlayerDamp { get; set; } = 3;
        [Dimension(1, -1, true)]
        public float JumpVelY { get; set;  } = 55;
        [Dimension(1, -1)]
        public float DashVelX { get; set; } = 50;
        [Dimension(1, -1, true)]
        public float DashVelY { get; set; } = 20;
        public float DashTimeout { get; set; } = 0.2f;
        public float DashCooltime { get; set; } = 1.3f;
        [Dimension(1, 0)]
        public float Width { get; set; } = 100;
        [Dimension(1, 0)]
        public float GoalWidth { get; set; } = 5;
        [Dimension(1, 0, true)]
        public float GoalHeight { get; set; } = 15;
        [Dimension(1, 0)]
        public Vector2 Spawn1 { get; set; } = new Vector2(8, 30);
        [Dimension(1, 0)]
        public Vector2 Spawn2 { get; set; } = new Vector2(92, 30);
        [Dimension(1, 0)]
        public float PlayerRadius { get; set; } = 1.5f;
        [Dimension(1, 0)]
        public Vector2 BallSpawn { get; set; } = new Vector2(50, 30);
        [Dimension(1, 0)]
        public float BallRadius { get; set; } = 1.5f;
        public float BounceCoeff { get; set; } = 0.8f;
        [Dimension(0,-1)]
        public float BallDamp { get; set; } = 0.8f;
        [Dimension(1, -1)]
        public float DoubleJmpVel { get; set; } = 20;
        [Dimension(1, -1, true)]
        public float BallCollideBounceVel { get; set; } = 45;
        public float PlayerBallCoupling { get; set; } = 1.8f;
        [Dimension(1, -1, true)]
        public float PlayerBallBounceY { get; set; } = 25;
        [Dimension(1, -1)]
        public float KickVel { get; set; } = 10;

        public float RadiusShrink { get; set; } = 0.8f;

        public GameConfigJson GetConfigJson() => new GameConfigJson(Width, GoalWidth, GoalHeight, PlayerRadius * RadiusShrink, BallRadius * RadiusShrink, previewTime);

        public static GameConfig Rescale(GameConfig config, float pl, float pt)
        {
            GameConfig newconfig = new GameConfig();
            foreach(var prop in typeof(GameConfig).GetProperties())
            {
                var attrs = prop.GetCustomAttributes(true);
                var val = prop.GetValue(config);
                prop.SetValue(newconfig, val);
                if ((attrs.Length > 0) && (attrs[0] is DimensionAttribute dimatt))
                {
                    if(val is float fval)
                    {
                        var lfactor = dimatt.IsY ? (float)Math.Pow(pl, dimatt.Length): 1;
                        prop.SetValue(newconfig, fval * lfactor * (float)Math.Pow(pt, dimatt.Time));
                    }
                    else if(val is Vector2 vval)
                    {
                        var tmp = vval * (float)Math.Pow(pt, dimatt.Time);
                        prop.SetValue(newconfig, new Vector2(tmp.X, tmp.Y*(float)Math.Pow(pl, dimatt.Length)));
                    }
                }                
            }

            return newconfig;
        }

        public static GameConfig ClassicConfig()
        {
            return new GameConfig()
            {
                MaxScore = 10,
                Gravity = 1,
                PlayerAcc = 1,
                PlayerDamp = 0.1f,
                JumpVelY = 12,
                DashVelX = 10,
                DashVelY = 3,
                Width = 460,
                GoalWidth = 25,
                GoalHeight = 90,
                Spawn1 = new Vector2(30, 20),
                Spawn2 = new Vector2(430, 20),
                BallSpawn = new Vector2(230, 150),
                PlayerRadius = 10,
                BallRadius = 10,
                BounceCoeff = 0.7f,
                BallDamp = 0.03f,
                DoubleJmpVel = 5,
                BallCollideBounceVel = 15,
                PlayerBallCoupling = 4f,
                PlayerBallBounceY = 10,
                KickVel = 1,
                RadiusShrink = 0.8f
            };
        }
    }
}
