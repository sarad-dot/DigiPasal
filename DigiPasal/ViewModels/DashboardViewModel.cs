using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _fullName = string.Empty;
        private string _shopName = string.Empty;
        private string _greeting = string.Empty;

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

        public string Greeting
        {
            get => _greeting;
            set => SetProperty(ref _greeting, value);
        }

        public ICommand NavigateToSalesCommand { get; }
        public ICommand NavigateToProductsCommand { get; }
        public ICommand NavigateToCreditCommand { get; }
        public ICommand LogoutCommand { get; }

        public DashboardViewModel(AuthService authService)
        {
            _authService = authService;
            Title = "Dashboard";

            NavigateToSalesCommand = new Command(async () => await Shell.Current.GoToAsync("//Sales"));
            NavigateToProductsCommand = new Command(async () => await Shell.Current.GoToAsync("//Products"));
            NavigateToCreditCommand = new Command(async () => await Shell.Current.GoToAsync("//Credit"));
            LogoutCommand = new Command(Logout);
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
                    Greeting = $"Welcome, {user.FullName}";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void Logout()
        {
            _authService.SignOut();
            await Shell.Current.GoToAsync("//Login");
        }
    }
}