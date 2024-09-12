using System.Numerics;

namespace PenFootball_GameServer.GameLogic
{
    public interface IMask
    {
        //CircMask BoundingCirc { get; }
        IMask TranslateTransform(Vector2 translation);
    }
    public interface ISimpleMask : IMask { }

    public static class Mask
    {
        public class CircMask : ISimpleMask
        {
            public CircMask(Vector2 center, float radius)
            {
                Radius = radius; Center = center;
            }
            public float Radius { get; }
            public Vector2 Center { get; }
            //public CircMask BoundingCirc { get => this; }
            public IMask TranslateTransform(Vector2 translation) => new CircMask(this.Center+translation, this.Radius);
        }
        public class LineMask : ISimpleMask
        {
            public LineMask(Vector2 pos1, Vector2 pos2)
            {
                Pos1 = pos1; Pos2 = pos2; Dir = Vector2.Normalize(pos2 - pos1); Normal = new Vector2(Dir.Y, -Dir.X);
                Length = (pos2 - pos1).Length();
                //BoundingCirc = new CircMask((Pos1 + Pos2) / 2, (Pos2 - Pos1).Length() / 2);
            }
            public Vector2 Pos1 { get; }
            public Vector2 Pos2 { get; }
            public float Length { get; }
            public Vector2 Dir { get; }
            public Vector2 Normal { get; }
            //public CircMask BoundingCirc { get; }
            public IMask TranslateTransform (Vector2 translation) => new LineMask(Pos1 + translation, Pos2 + translation);
        }
        public class CompositeMask : IMask
        {
            public CompositeMask(IEnumerable<ISimpleMask> innerMasks)
            {
                InnerMasks = innerMasks;    
                //BoundingCirc = 
            }
            public IEnumerable<ISimpleMask> InnerMasks { get; }
            //public CircMask BoundingCirc { get; }
            public IMask TranslateTransform(Vector2 translation) => new CompositeMask(InnerMasks.Select(mask => (ISimpleMask)(mask.TranslateTransform(translation))));
        }

        private static bool ChkCollide_CC(CircMask m1, CircMask m2)
        {
            return (m1.Center - m2.Center).Length() < (m1.Radius + m2.Radius);
        }

        private static bool ChkCollide_CL(CircMask m1, LineMask m2)
        {
            var dist = Math.Abs(Vector2.Dot(m2.Pos1 - m1.Center, m2.Normal));
            if (dist > m1.Radius)
                return false;

            var centerdist = Vector2.Dot(m1.Center - m2.Pos1, m2.Dir);
            var pm = (float)Math.Sqrt(m1.Radius * m1.Radius - dist * dist);

            bool isinrange (float dis) { return 0 < dis && dis < m2.Length;}

            return isinrange(centerdist - pm) || isinrange(centerdist + pm);
        }
        private static bool ChkCollide_LL(LineMask m1, LineMask m2)
        {
            var v2 = m1.Pos2 - m1.Pos1;
            var v3 = m2.Pos1 - m1.Pos1;
            var v4 = m2.Pos2 - m1.Pos1;
            if (!Matrix3x2.Invert(new Matrix3x2(v3.X, v4.X, 0, v3.Y, v4.Y, 0), out Matrix3x2 invmat))
                return false;
            var tv2 = Vector2.Transform(v2, invmat);
            return (tv2.X > 0) && (tv2.Y > 0);
        }

        public static bool ChkCollide(ISimpleMask mask1, ISimpleMask mask2)
        {
            //if (ChkCollide_CC(mask1.BoundingCirc, mask2.BoundingCirc))
            //    return true;

            switch (mask1, mask2)
            {
                case (CircMask cm1, CircMask cm2):
                    return ChkCollide_CC(cm1, cm2);
                case (CircMask cm, LineMask lm):
                    return ChkCollide_CL(cm, lm);
                case (LineMask lm, CircMask cm):
                    return ChkCollide_CL(cm, lm);
                case (LineMask lm1, LineMask lm2):
                    return ChkCollide_LL(lm1, lm2);
            }
            return false;
        }

        private static IEnumerable<ISimpleMask> GetInnerMasks (IMask mask)
        {
            if (mask is ISimpleMask sm)
                return new List<ISimpleMask> { sm };
            else if (mask is CompositeMask cm)
                return cm.InnerMasks;
            else
                throw new ArgumentException();
        }

        public static bool ChkCollide(IMask mask1, IMask mask2)
        {
            foreach (ISimpleMask sm1 in GetInnerMasks(mask1))
            {
                foreach(ISimpleMask sm2 in GetInnerMasks(mask2))
                {
                    if(ChkCollide(sm1, sm2))
                        return true;
                }
            }
            return false;
        }
    }

    public class TranslatedMask
    {
        public TranslatedMask(IMask baseMask, Vector2 translation)
        {
            BaseMask = baseMask;
            Translation = translation;
        }

        public IMask BaseMask { get; set; }
        public Vector2 Translation { get; set; }

        public IMask GetMask => BaseMask.TranslateTransform(Translation);
        
        public void Translate(Vector2 translation)
        {
            this.Translation += translation;
        }
    }
}
