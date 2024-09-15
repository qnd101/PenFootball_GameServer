using System.Reflection;
using System.Text.RegularExpressions;

namespace PenFootball_GameServer.Services
{
    public class EntranceSettings
    {
        public List<Dictionary<string, string>> SettingsList { get; set; }


        public bool Validate(Dictionary<string, string> obj)
        {
            return SettingsList.Any(regexDict =>
            regexDict.All(prop =>
                {
                    string propertyName = prop.Key;
                    string regexPattern = prop.Value;
                    if(!obj.TryGetValue(propertyName, out var propertyValue))
                        return false;
                    if (propertyValue == null || !Regex.IsMatch(propertyValue, regexPattern))
                        return false;
                    return true;
                })
            );
        }
    }
}
