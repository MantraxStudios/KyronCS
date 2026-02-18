namespace KrayonHub;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;

    public LoginPage()
    {
        InitializeComponent();
        _authService = new AuthService();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = EntryLoginUsername.Text?.Trim();
        var password = EntryLoginPassword.Text?.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Error", "Please fill all fields", "OK");
            return;
        }

        try
        {
            var response = await _authService.Login(username, password);

            if (response.success)
            {
                SessionManager.SaveSession(response.user);
                Application.Current.MainPage = new MainPage();
            }
            else
            {
                await DisplayAlert("Error", response.message, "OK");
            }
        }
        catch
        {
            await DisplayAlert("Error", "Connection failed. Make sure the server is running.", "OK");
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var username = EntryRegUsername.Text?.Trim();
        var email = EntryRegEmail.Text?.Trim();
        var password = EntryRegPassword.Text?.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Error", "Please fill all fields", "OK");
            return;
        }

        try
        {
            var response = await _authService.Register(username, email, password);

            if (response.success)
            {
                await DisplayAlert("Success", "Registration successful! Please login.", "OK");
                OnSwitchToLogin(null, null);
            }
            else
            {
                await DisplayAlert("Error", response.message, "OK");
            }
        }
        catch
        {
            await DisplayAlert("Error", "Connection failed. Make sure the server is running.", "OK");
        }
    }

    private void OnSwitchToRegister(object sender, EventArgs e)
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = true;
    }

    private void OnSwitchToLogin(object sender, EventArgs e)
    {
        LoginView.IsVisible = true;
        RegisterView.IsVisible = false;
    }

    private void OnContinueWithoutAccount(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage();
    }
}