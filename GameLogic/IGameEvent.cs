namespace PenFootball_GameServer.GameLogic
{
    public enum GameKey
    {
        Up, Down, Left, Right
    }

    public enum KeyEventType
    {
        KeyUp, KeyDown
    }
    public interface IGameEvent { }

    public class KeyEvent : IGameEvent
    {
        public int WhichPlayer { get; }
        public GameKey Key { get; }
        public KeyEventType EventType { get; }

        public KeyEvent(int whichplayer, GameKey key, KeyEventType eventtype)
        {
            WhichPlayer = whichplayer;
            Key = key;
            EventType = eventtype;
        }
    }

    public class ExitEvent : IGameEvent 
    { 
        public int WhichPlayer { get; }

        public ExitEvent(int whichplayer)
        {
            WhichPlayer=whichplayer;
        }
    }

    public class ChatEvent : IGameEvent
    {
        public int WhichPlayer { get; }
        public string Msg { get; }
        public ChatEvent(int whichplayer, string msg)
        {
            WhichPlayer = whichplayer; Msg = msg;
        }
    }
}
