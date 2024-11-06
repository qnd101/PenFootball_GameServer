using PenFootball_GameServer.Services;

namespace PenFootball_GameServer.Hubs
{
    public interface IGameClient
    {
        Task InvalidToken();
        //Training의 경우 GameFound는 안씀. 
        Task GameFound(int userid);
        Task UpdateFrame(string userName);  

        Task Preview(PreviewOutput output);
        Task GameEnd(GameEndOutput geo);
        Task Chat(ChatOutput co);
        Task GlobalChat(ChatObj cobj);

    }
}
