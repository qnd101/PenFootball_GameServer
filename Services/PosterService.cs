using Microsoft.Extensions.Options;
using PenFootball_GameServer.Settings;
using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace PenFootball_GameServer.Services
{
    public interface IPosterService
    {
        Task<bool> PostJSON(string path, object data);
    }
    public class PosterService : IPosterService
    {
        private ConnectionSettings _conSettings;
        private HttpClient _httpClient;
        private ILogger<PosterService> _logger;
        private string _token;
        private static readonly int SafeMinutes = 5; //Expiriation 5분 전부터 토큰 재발급

        public PosterService(IOptions<ConnectionSettings> conSettngs, ILogger<PosterService> logger)
        {
            _conSettings = conSettngs.Value;
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(conSettngs.Value.Path),
            }; 
            _logger = logger;
        }
        public async Task<bool> PostJSON(string endpoint, object data)
        {
            async Task<bool> tempPostJSON(string tendpoint, object tdata)
            {
                _logger.LogInformation($"Current Token: {_token}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                string json = JsonSerializer.Serialize(tdata);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    var response = await _httpClient.PostAsync(tendpoint, content);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    //Authentication Failed
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("Cannot Authenticate to Server.");
                        return false;
                    }
                    else
                    {
                        var message = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Post Operation towards main server FAILED. Status Code : {response.StatusCode}, Content: {message}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    return false;
                }
            }
            
            if (!await tempPostJSON(endpoint, data))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                _logger.LogInformation("Token Expired. Reissuing...");
                string loginjson = JsonSerializer.Serialize(new { Username = _conSettings.Username, Password = _conSettings.Password });
                try
                {
                    var loginresponse = await _httpClient.PostAsync("api/users/login", new StringContent(loginjson, Encoding.UTF8, "application/json"));
                    if (loginresponse.IsSuccessStatusCode)
                    {
                        var newtoken = await loginresponse.Content.ReadAsStringAsync();
                        _token = newtoken;
                        return await tempPostJSON(endpoint, data);
                    }
                    else
                    {
                        _logger.LogInformation($"Error in Login: {await loginresponse.Content.ReadAsStringAsync()}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    return false;
                }
            }
            return true;
        }
    }
}
