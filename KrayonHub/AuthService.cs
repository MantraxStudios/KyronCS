using System.Text;
using System.Text.Json;

namespace KrayonHub;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "http://localhost/krayon_api/";

    public AuthService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<AuthResponse> Register(string username, string email, string password)
    {
        var data = new { username, email, password };
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BASE_URL}register.php", content);
        var result = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(result);
    }

    public async Task<AuthResponse> Login(string username, string password)
    {
        var data = new { username, password };
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BASE_URL}login.php", content);
        var result = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(result);
    }
}

public class AuthResponse
{
    public bool success { get; set; }
    public string message { get; set; }
    public User user { get; set; }
}

public class User
{
    public int id { get; set; }
    public string username { get; set; }
    public string email { get; set; }
}