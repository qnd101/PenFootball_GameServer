using System.Numerics;
using System.Security.Cryptography;

namespace PenFootball_GameServer.GameLogic
{
    public class TimeDimAttribute : Attribute
    {
        public float Time { get; set; }
        public TimeDimAttribute(float time) {
            Time = time;
        }

        public static void Rescale<T>(T config, float pt)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                var attrs = prop.GetCustomAttributes(true);
                var val = prop.GetValue(config);
                prop.SetValue(config, val);
                if ((attrs.Length > 0) && (attrs[0] is TimeDimAttribute dimatt))
                {
                    if (val is float fval)
                    {
                        prop.SetValue(config, fval * (float)Math.Pow(pt, dimatt.Time));
                    }
                    else if (val is Vector2 vval)
                    {
                        prop.SetValue(config, vval * (float)Math.Pow(pt, dimatt.Time));
                    }
                }
            }
        }
    }

    public record GeoConfigJson(float width, float goalWidth, float goalHeight, float playerRadius, float ballRadius);


    //physical constants
    public class PhysConfig
    {
        [TimeDim(-2)]
        public float Gravity { get; set; } = 130;
        [TimeDim(-2)]
        public float PlayerAcc { get; set; } = 120;
        [TimeDim(-1)]
        public float PlayerDamp { get; set; } = 3;
        [TimeDim(-1)]
        public float JumpVelY { get; set;  } = 55;
        [TimeDim(-1)]
        public float DashVelX { get; set; } = 50;
        [TimeDim(-1)]
        public float DashVelY { get; set; } = 20;
        public float DashTimeout { get; set; } = 0.2f;
        public float DashCooltime { get; set; } = 1.3f;
        public float Width { get; set; } = 100;
        public float GoalWidth { get; set; } = 5;
        public float GoalHeight { get; set; } = 15;
        public float PlayerRadius { get; set; } = 1.5f;
        public float BallRadius { get; set; } = 1.5f;
        public float BounceCoeff { get; set; } = 0.8f;
        [TimeDim(-1)]
        public float BallDamp { get; set; } = 0.8f;
        [TimeDim(-1)]
        public float DoubleJmpVel { get; set; } = 20;
        [TimeDim(-1)]
        public float BallCollideBounceVel { get; set; } = 45;
        public float PlayerBallCoupling { get; set; } = 1.8f;
        [TimeDim(-1)]
        public float PlayerBallBounceY { get; set; } = 25;
        [TimeDim(-1)]
        public float KickVel { get; set; } = 10;
        public float RadiusShrink { get; set; } = 0.8f;

        public GeoConfigJson GetConfigJson() => new GeoConfigJson(Width, GoalWidth, GoalHeight, PlayerRadius * RadiusShrink, BallRadius * RadiusShrink);

        public static PhysConfig ClassicConfig()
        {
            return new PhysConfig()
            {
                Gravity = 1,
                PlayerAcc = 1,
                PlayerDamp = 0.1f,
                JumpVelY = 12,
                DashVelX = 10,
                DashVelY = 3,
                Width = 460,
                GoalWidth = 25,
                GoalHeight = 90,
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
