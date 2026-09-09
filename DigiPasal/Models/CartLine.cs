using System.ComponentModel;
using System.Runtime.CompilerServices;
using DigiPasal.Services;

namespace DigiPasal.Models;

public class CartLine : INotifyPropertyChanged
{
    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Unit { get; init; } = "pcs";

    public decimal UnitPrice { get; init; }

    public decimal CostPrice { get; init; }

    public int StockQuantity { get; init; }

    private double _quantity;

    public double Quantity
    {
        get => _quantity;
        set
        {
            if (Math.Abs(_quantity - value) < 0.0000001)
                return;

            _quantity = value;
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(QuantityDisplay));
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(LineTotalDisplay));
        }
    }

    public string QuantityDisplay => UnitsHelper.FormatQuantity(Quantity);

    public decimal LineTotal => (decimal)Quantity * UnitPrice;

    public string LineTotalDisplay => CurrencyFormatter.Format(LineTotal);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}