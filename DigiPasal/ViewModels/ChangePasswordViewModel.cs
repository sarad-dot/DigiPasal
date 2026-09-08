using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class ChangePasswordViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private bool _isCurrentPasswordHidden = true;
        private bool _isNewPasswordHidden = true;

        public string CurrentPassword
        {
            get => _currentPassword;
            set { SetProperty(ref _currentPassword, value); ClearError(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { SetProperty(ref _newPassword, value); ClearError(); }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { SetProperty(ref _confirmPassword, value); ClearError(); }
        }

        public bool IsCurrentPasswordHidden
        {
            get => _isCurrentPasswordHidden;
            set
            {
                if (SetProperty(ref _isCurrentPasswordHidden, value))
                    OnPropertyChanged(nameof(CurrentPasswordVisibilityLabel));
            }
        }

        public bool IsNewPasswordHidden
        {
            get => _isNewPasswordHidden;
            set
            {
                if (SetProperty(ref _isNewPasswordHidden, value))
                    OnPropertyChanged(nameof(NewPasswordVisibilityLabel));
            }
        }

        public string CurrentPasswordVisibilityLabel => IsCurrentPasswordHidden ? "Show" : "Hide";

        public string NewPasswordVisibilityLabel => IsNewPasswordHidden ? "Show" : "Hide";

        public ICommand ChangePasswordCommand { get; }
        public ICommand ToggleCurrentPasswordVisibilityCommand { get; }
        public ICommand ToggleNewPasswordVisibilityCommand { get; }

        public ChangePasswordViewModel(AuthService authService)
        {
            _authService = authService;

            ChangePasswordCommand = new Command(async () => await ChangePassword());
            ToggleCurrentPasswordVisibilityCommand = new Command(() => IsCurrentPasswordHidden = !IsCurrentPasswordHidden);
            ToggleNewPasswordVisibilityCommand = new Command(() => IsNewPasswordHidden = !IsNewPasswordHidden);
        }

        private async Task ChangePassword()
        {
            if (string.IsNullOrEmpty(CurrentPassword))
            {
                SetError("Enter your current password");
                return;
            }

            if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 8)
            {
                SetError("New password must be at least 8 characters");
                return;
            }

            if (NewPassword == CurrentPassword)
            {
                SetError("New password must be different from the current password");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                SetError("New passwords do not match");
                return;
            }

            string? username = _authService.CurrentUsername;
            if (string.IsNullOrEmpty(username))
            {
                SetError("Could not identify the current user");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                bool success = await _authService.ChangePasswordAsync(username, CurrentPassword, NewPassword);
                if (success)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Password Changed",
                        "Your password has been updated successfully.",
                        "OK");

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    SetError("Current password is incorrect");
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