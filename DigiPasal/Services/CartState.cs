using System.Collections.ObjectModel;
using DigiPasal.Models;

namespace DigiPasal.Services;

public class CartState
{
    private static readonly Lazy<CartState> _lazyInstance = new(() => new CartState());

    public static CartState Instance => _lazyInstance.Value;

    private CartState() { }

    public ObservableCollection<CartLine> Items { get; } = new();

    public event Action? CartChanged;

    public void AddProduct(Product product)
    {
        var existing = Items.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
        }
        else
        {
            Items.Add(new CartLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                UnitPrice = product.SellingPrice,
                CostPrice = product.CostPrice,
                Quantity = 1
            });
        }

        CartChanged?.Invoke();
    }

    public void RemoveItem(CartLine item)
    {
        Items.Remove(item);
        CartChanged?.Invoke();
    }

    public void Clear()
    {
        Items.Clear();
        CartChanged?.Invoke();
    }

    public int ItemCount => Items.Count;

    public decimal Subtotal => Items.Sum(c => c.LineTotal);
}
