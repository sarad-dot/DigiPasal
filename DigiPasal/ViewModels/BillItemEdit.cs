using System.Collections.ObjectModel;
using DigiPasal.Models;

namespace DigiPasal.ViewModels;

public class BillItemEdit : BaseViewModel
{
    public ObservableCollection<Product> Products { get; }

    private Product? _selectedProduct;
    private string _quantityText = "1";
    private string _costText = string.Empty;

    public BillItemEdit(ObservableCollection<Product> products)
    {
        Products = products;
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (_selectedProduct != null && value != null &&
                _selectedProduct.Id == value.Id)
                return;

            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(LineTotal));
                if (value != null && string.IsNullOrWhiteSpace(CostText))
                    CostText = value.CostPrice > 0 ? value.CostPrice.ToString("0.##") : string.Empty;
            }
        }
    }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (SetProperty(ref _quantityText, value))
                OnPropertyChanged(nameof(LineTotal));
        }
    }

    public string CostText
    {
        get => _costText;
        set
        {
            if (SetProperty(ref _costText, value))
                OnPropertyChanged(nameof(LineTotal));
        }
    }

    public double Quantity =>
        double.TryParse(QuantityText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture, out var q)
            ? q
            : 0;

    public decimal CostPrice =>
        decimal.TryParse(CostText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture, out var c)
            ? c
            : 0;

    public decimal LineTotal => (decimal)Quantity * CostPrice;
}