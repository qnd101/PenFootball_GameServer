using System.Reflection;
using System.Text.RegularExpressions;

namespace PenFootball_GameServer.Services
{
    public class EntranceSettings
    {
        public List<Dictionary<string, string>> SettingsList { get; set; }

        public bool Validate<T>(T obj)
        {
            // Loop through each key-value pair in the regex dictionary
            foreach (var regexDict in SettingsList)
            {
                if (regexDict.All(property =>
                {
                    string propertyName = property.Key;
                    string regexPattern = property.Value;

                    // Use reflection to get the property value from the object
                    PropertyInfo? propInfo = typeof(T).GetProperty(propertyName);

                    if (propInfo == null)
                        return false;

                    string? propertyValue = propInfo.GetValue(obj)?.ToString();

                    // If the property is null or doesn't match the regex, mark it as invalid
                    if (propertyValue == null || !Regex.IsMatch(propertyValue, regexPattern))
                        return false;
                    return true;
                }))
                    return true;
            }
            return false;
        }

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
