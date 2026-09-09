using DigiPasal.Services;

namespace DigiPasal;

public partial class MainPage : ContentPage
{
    private readonly AuthService _authService;
    private bool _hasRouted;

    public MainPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasRouted)
            return;

        _hasRouted = true;

        try
        {
            await App.StartupInitialization;
            await _authService.RestoreSessionAsync();

            if (_authService.IsAuthenticated)
            {
                await Task.Delay(50);
                await Shell.Current.GoToAsync("//Dashboard");
                return;
            }

            bool hasUser = await _authService.HasAnyUserAsync();

            await Task.Delay(50);
            await Shell.Current.GoToAsync(hasUser ? "//Login" : "//Register");
        }
        catch (Exception ex)
        {
            LoadingIndicator.IsRunning = false;
            await DisplayAlertAsync("Startup error", ex.Message, "OK");
        }
    }

    protected override bool OnBackButtonPressed()
    {
#if ANDROID
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
        return true;
    }
}