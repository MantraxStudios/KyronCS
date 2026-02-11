using System.Text.Json;

namespace KrayonHub;

public static class SessionManager
{
    private static string SessionFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KrayonHub",
        "session.json"
    );

    public static void SaveSession(User user)
    {
        var directory = Path.GetDirectoryName(SessionFilePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(user);
        File.WriteAllText(SessionFilePath, json);
    }

    public static User GetSession()
    {
        if (!File.Exists(SessionFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(SessionFilePath);
            return JsonSerializer.Deserialize<User>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearSession()
    {
        if (File.Exists(SessionFilePath))
            File.Delete(SessionFilePath);
    }

    public static bool IsLoggedIn()
    {
        return GetSession() != null;
    }
}