﻿using System.Numerics;
using System.Runtime.CompilerServices;

namespace PenFootball_GameServer.GameLogic
{
    //Frame object that serializes into JSON object

    public record VectorForJSON(float x, float y)
    {
        public static VectorForJSON FromVec2(System.Numerics.Vector2 vec) => new VectorForJSON(vec.X, vec.Y);
    }

    public record Frame(VectorForJSON player1, VectorForJSON player2, VectorForJSON ball)
    {
        public static Frame Flip(Frame frame, float width)
        {
            VectorForJSON FlipVector(VectorForJSON vec)
            {
                return new VectorForJSON(width - vec.x, vec.y);
            }
            return new Frame(FlipVector(frame.player2), FlipVector(frame.player1), FlipVector(frame.ball));
        }
    }
}