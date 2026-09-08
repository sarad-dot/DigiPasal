using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class ForgotPasswordViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _username = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmNewPassword = string.Empty;
        private bool _isVerified;

        public string Username
        {
            get => _username;
            set { SetProperty(ref _username, value); ClearError(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { SetProperty(ref _newPassword, value); ClearError(); }
        }

        public string ConfirmNewPassword
        {
            get => _confirmNewPassword;
            set { SetProperty(ref _confirmNewPassword, value); ClearError(); }
        }

        public bool IsVerified
        {
            get => _isVerified;
            set
            {
                if (SetProperty(ref _isVerified, value))
                    OnPropertyChanged(nameof(IsNotVerified));
            }
        }

        public bool IsNotVerified => !_isVerified;

        public ICommand VerifyCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand NavigateToLoginCommand { get; }

        public ForgotPasswordViewModel(AuthService authService)
        {
            _authService = authService;

            VerifyCommand = new Command(async () => await VerifyIdentity());
            ResetPasswordCommand = new Command(async () => await ResetPassword());
            NavigateToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//Login"));
        }

        private async Task VerifyIdentity()
        {
            if (string.IsNullOrEmpty(Username))
            {
                SetError("Please enter your username");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                var user = await _authService.GetUserByUsernameAsync(Username);
                if (user == null)
                {
                    SetError("User not found");
                    return;
                }

                if (!await _authService.HasDeviceLockScreenAsync())
                {
                    SetError("Please set up a device lock screen first");
                    return;
                }

                bool authenticated = await _authService.AuthenticateWithDeviceLockAsync();
                if (authenticated)
                {
                    IsVerified = true;
                    await Shell.Current.DisplayAlertAsync("Success", "Identity verified! Set your new password.", "OK");
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

        private async Task ResetPassword()
        {
            if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 8)
            {
                SetError("Password must be at least 8 characters");
                return;
            }

            if (NewPassword != ConfirmNewPassword)
            {
                SetError("Passwords do not match");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                bool success = await _authService.ResetPasswordAsync(Username, NewPassword);
                if (success)
                {
                    await Shell.Current.DisplayAlertAsync("Success", "Password reset successfully! Please sign in.", "OK");
                    IsVerified = false;
                    NewPassword = string.Empty;
                    ConfirmNewPassword = string.Empty;
                    await Shell.Current.GoToAsync("//Login");
                }
                else
                {
                    SetError("Failed to reset password. Please try again.");
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