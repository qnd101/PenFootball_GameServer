using Microsoft.AspNetCore.SignalR;
using PenFootball_GameServer.Hubs;
using System.Collections.Concurrent;

namespace PenFootball_GameServer.Services
{
    public enum ChatResult
    {
        Success = 0,
        Timeout = 1,
        TooLong = 2
    }

    public interface IGlobalChatService
    {
        ChatResult AddChat(string name, string msg);
        ChatObj[] GetCache();
    }
    public class GlobalChatService : IGlobalChatService
    {
        private ConcurrentQueue<(string name, string msg, DateTime sendtime)> _chatqueue = new ConcurrentQueue<(string name, string msg, DateTime sendtime)>();

        private static int _maxchatcache = 10;
        private static int _chattimeout = 1000;
        private static int _chatmaxlen = 30;

        public GlobalChatService()
        {
            _chatqueue.Enqueue(("SSHSPhys", "<server reset>", DateTime.Now));
        }

        public ChatResult AddChat(string name, string msg)
        {
            if (msg.Length > _chatmaxlen)
                return ChatResult.TooLong;
            var beftime = _chatqueue.Where(item => item.name == name).DefaultIfEmpty().Max(item=>item.sendtime);
            if (beftime != default && (DateTime.Now - beftime).TotalMilliseconds < _chattimeout)
                return ChatResult.Timeout;

            _chatqueue.Enqueue((name, msg, DateTime.Now));
            if (_maxchatcache <= _chatqueue.Count)
            {
                _chatqueue.TryDequeue(out _);
            }
            return ChatResult.Success;
        }

        public ChatObj[] GetCache()
        {
            return _chatqueue.Select(item => new ChatObj(item.name, item.msg, $"{item.sendtime.Hour}:{item.sendtime.Minute}")).ToArray();
        }
    }
}
