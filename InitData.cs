namespace PenFootball_GameServer
{
    public record InitData(string Secret, List<Dictionary<string, string>> EntrancePolicy);
}
