namespace KrayonHub;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        if (SessionManager.IsLoggedIn())
        {
            MainPage = new MainPage();
        }
        else
        {
            MainPage = new LoginPage();
        }
    }
}