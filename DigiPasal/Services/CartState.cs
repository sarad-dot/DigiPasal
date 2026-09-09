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

    public void AddProduct(Product product, double quantity = 1)
    {
        if (quantity <= 0) quantity = 1;

        var existing = Items.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            var maxCanAdd = existing.StockQuantity - existing.Quantity;
            if (maxCanAdd <= 0)
                return;

            existing.Quantity += Math.Min(quantity, maxCanAdd);
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
                StockQuantity = (int)product.StockQuantity,
                Quantity = Math.Min(quantity, product.StockQuantity)
            });
        }

        CartChanged?.Invoke();
    }

    public bool IncrementQuantity(CartLine item)
    {
        if (item.Quantity >= item.StockQuantity)
            return false;

        item.Quantity += 1;
        CartChanged?.Invoke();
        return true;
    }

    public void DecrementQuantity(CartLine item)
    {
        if (item.Quantity <= 1)
        {
            Items.Remove(item);
        }
        else
        {
            item.Quantity -= 1;
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
