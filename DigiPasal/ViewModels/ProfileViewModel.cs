using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class ProfileViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _fullName = string.Empty;
        private string _shopName = string.Empty;
        private string _username = string.Empty;
        private string _initials = string.Empty;
        private string _memberSince = string.Empty;
        private string _editFullName = string.Empty;
        private string _editShopName = string.Empty;
        private bool _isEditing;

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string ShopName
        {
            get => _shopName;
            set => SetProperty(ref _shopName, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Initials
        {
            get => _initials;
            set => SetProperty(ref _initials, value);
        }

        public string MemberSince
        {
            get => _memberSince;
            set => SetProperty(ref _memberSince, value);
        }

        public string EditFullName
        {
            get => _editFullName;
            set => SetProperty(ref _editFullName, value);
        }

        public string EditShopName
        {
            get => _editShopName;
            set => SetProperty(ref _editShopName, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(IsNotEditing));
            }
        }

        public bool IsNotEditing => !_isEditing;

        public ICommand BeginEditCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand LogoutCommand { get; }

        public ProfileViewModel(AuthService authService)
        {
            _authService = authService;
            Title = "Profile";

            BeginEditCommand = new Command(BeginEdit);
            SaveEditCommand = new Command(async () => await SaveEdit());
            CancelEditCommand = new Command(CancelEdit);
            ChangePasswordCommand = new Command(async () => await Shell.Current.GoToAsync("ChangePassword"));
            LogoutCommand = new Command(async () => await Logout());
        }

        public async Task LoadUserAsync()
        {
            IsBusy = true;
            try
            {
                User? user = await _authService.GetCurrentUserAsync();
                if (user != null)
                {
                    FullName = user.FullName;
                    ShopName = user.ShopName;
                    Username = "@" + user.Username;
                    Initials = BuildInitials(user.FullName);
                    MemberSince = user.CreatedAt != default
                        ? user.CreatedAt.ToLocalTime().ToString("MMMM yyyy")
                        : string.Empty;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BeginEdit()
        {
            EditFullName = FullName;
            EditShopName = ShopName;
            ClearError();
            IsEditing = true;
        }

        private async Task SaveEdit()
        {
            if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditShopName))
            {
                SetError("Full name and shop name are required");
                return;
            }

            IsBusy = true;
            ClearError();

            try
            {
                string? username = _authService.CurrentUsername;
                if (string.IsNullOrEmpty(username))
                {
                    SetError("Could not identify the current user");
                    return;
                }

                bool success = await _authService.UpdateProfileAsync(username, EditFullName, EditShopName);
                if (success)
                {
                    FullName = EditFullName.Trim();
                    ShopName = EditShopName.Trim();
                    Initials = BuildInitials(FullName);
                    IsEditing = false;
                }
                else
                {
                    SetError("Failed to update profile. Please try again.");
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

        private void CancelEdit()
        {
            ClearError();
            IsEditing = false;
        }

        private async Task Logout()
        {
            _authService.SignOut();
            await Shell.Current.GoToAsync("//Login");
        }

        private static string BuildInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0][..1].ToUpperInvariant();

            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}