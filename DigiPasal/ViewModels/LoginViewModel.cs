using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _isPasswordHidden = true;

        public string Username
        {
            get => _username;
            set { SetProperty(ref _username, value); ClearError(); }
        }

        public string Password
        {
            get => _password;
            set { SetProperty(ref _password, value); ClearError(); }
        }

        public bool IsPasswordHidden
        {
            get => _isPasswordHidden;
            set
            {
                if (SetProperty(ref _isPasswordHidden, value))
                    OnPropertyChanged(nameof(PasswordVisibilityLabel));
            }
        }

        public string PasswordVisibilityLabel => IsPasswordHidden ? "Show" : "Hide";

        public ICommand LoginCommand { get; }
        public ICommand DeviceLockCommand { get; }
        public ICommand GoogleLoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand NavigateToRegisterCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;

            LoginCommand = new Command(async () => await Login());
            DeviceLockCommand = new Command(async () => await DeviceLockLogin());
            GoogleLoginCommand = new Command(async () => await GoogleLogin());
            ForgotPasswordCommand = new Command(async () => await Shell.Current.GoToAsync("ForgotPassword"));
            NavigateToRegisterCommand = new Command(async () => await Shell.Current.GoToAsync("//Register"));
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        }

        private async Task Login()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                SetError("Please enter username and password");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                if (_authService.IsLockedOut)
                {
                    SetError($"Too many failed attempts. Try again in {_authService.LockoutRemaining.TotalSeconds:0} seconds.");
                    return;
                }

                User? user = await _authService.LoginAsync(Username, Password);
                if (user != null)
                {
                    await Shell.Current.GoToAsync("//Dashboard");
                }
                else
                {
                    if (_authService.IsLockedOut)
                    {
                        SetError($"Too many failed attempts. Try again in {_authService.LockoutRemaining.TotalSeconds:0} seconds.");
                    }
                    else
                    {
                        SetError("Invalid username or password");
                    }
                }
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GoogleLogin()
        {
            IsBusy = true;
            ClearError();

            try
            {
                SetError("Google login coming soon.");
                await Task.CompletedTask;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeviceLockLogin()
        {
            IsBusy = true;
            ClearError();

            try
            {
                if (!await _authService.HasDeviceLockScreenAsync())
                {
                    SetError("Please set up a device lock screen (PIN/Pattern/Password) first");
                    return;
                }

                bool authenticated = await _authService.AuthenticateWithDeviceLockAsync();
                if (authenticated)
                {
                    if (_authService.CurrentUser != null)
                    {
                        await Shell.Current.GoToAsync("//Dashboard");
                    }
                    else
                    {
                        SetError("No account found. Please register first.");
                    }
                }
                else
                {
                    SetError("Authentication failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}