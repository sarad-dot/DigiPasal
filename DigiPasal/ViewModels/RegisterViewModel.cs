using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class RegisterViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _fullName = string.Empty;
        private string _shopName = string.Empty;
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

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { SetProperty(ref _confirmPassword, value); ClearError(); }
        }

        public string FullName
        {
            get => _fullName;
            set { SetProperty(ref _fullName, value); ClearError(); }
        }

        public string ShopName
        {
            get => _shopName;
            set { SetProperty(ref _shopName, value); ClearError(); }
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

        public ICommand RegisterCommand { get; }
        public ICommand NavigateToLoginCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }

        public RegisterViewModel(AuthService authService)
        {
            _authService = authService;

            RegisterCommand = new Command(async () => await Register());
            NavigateToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//Login"));
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        }

        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                SetError("Username is required");
                return;
            }

            if (string.IsNullOrEmpty(Password) || Password.Length < 8)
            {
                SetError("Password must be at least 8 characters");
                return;
            }

            if (Password != ConfirmPassword)
            {
                SetError("Passwords do not match");
                return;
            }

            if (string.IsNullOrWhiteSpace(FullName))
            {
                SetError("Full name is required");
                return;
            }

            if (string.IsNullOrWhiteSpace(ShopName))
            {
                SetError("Shop name is required");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                await _authService.RegisterUserAsync(Username, Password, FullName, ShopName);

                User? user = await _authService.LoginAsync(Username, Password);
                if (user != null)
                {
                    await Shell.Current.GoToAsync("//Dashboard");
                }
                else
                {
                    SetError("Registration successful. Please sign in.");
                    await Shell.Current.GoToAsync("//Login");
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