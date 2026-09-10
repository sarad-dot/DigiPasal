# DigiPasal Project Log

## 2026-09-10 — Low-end Android performance pass

### Change
Targeted the app's biggest jank/memory costs for budget devices. No behavior or UI changes other than visuals described below.

### Files
- `Views/SalePage.xaml` — removed `Shadow` from the product row template (card now uses a solid stroke); removed `SnapPointsType="Mandatory"` from the vertical product list
- `Views/CartPage.xaml`, `Views/ProductPage.xaml`, `Views/SalesHistoryPage.xaml`, `Views/CreditPage.xaml` — removed `Shadow` from all scrolling item templates (same solid-stroke card look)
- `ViewModels/Debouncer.cs` — new; cancels a pending call and re-schedules after a delay on the UI thread
- `ViewModels/SaleViewModel.cs`, `ViewModels/ProductListViewModel.cs` — search/category filter is debounced (250 ms) so the list is not rebuilt per keystroke
- `ViewModels/SalesHistoryViewModel.cs` — search debounced (300 ms) so the DB is not re-queried per keystroke
- `Services/SaleService.cs` — text filter moved into SQLite (`LIKE` on ReceiptNumber/CustomerName) instead of loading the whole window in memory; results capped with `Take(500)`; `GetDailySalesSummaryAsync` now loads all `SaleItems` with a single `IN` query (was N+1)
- `Services/ProductService.cs` — `GetCategoriesAsync` now uses `SELECT DISTINCT Category` instead of loading all products
- `Services/BackupService.cs` — added `CreateBackupIfDueAsync(minAge)`
- `App.xaml.cs` — startup auto-backup throttled to once per 12 h; manual "Create backup" in Settings still runs immediately

### Notes
- Shadows remain only on fixed (non-scrolling) cards and bars; scrolling rows use `StrokeThickness="1"` instead.
- With the 500-row cap, the Sales History totals reflect the capped subset only for unusually large windows; the default 30-day view is unaffected.
- Release build flags were reviewed: XAML source-gen + compiled bindings already enabled; trimming + profiled AOT defaults kept. Do not enable full AOT (APK grows 2-3x).

### Verify
1. Build in Rider (no .NET 10 SDK on the CLI box).
2. Type quickly in Sale / Stock search — list now updates ~250 ms after you pause, with no per-keystroke rebuild.
3. Scroll product/history/credit lists — smoother on low-end hardware after removing template shadows.
4. Launch app twice within 12 h — no new backup file is created on the second launch.

---

## 2026-09-10 — Calculator-style Quick Sale

### Change
Rebuilt Quick Sale as a calculator pad. Sale amount is a **running sum** (digits → `+` → accumulate). Completing the sale auto-folds any pending entry into the total.

### Files
- `Views/QuickSalePage.xaml` — display + numpad + compact customer/credit strip
- `ViewModels/QuickSaleViewModel.cs` — digit / decimal / backspace / clear / add-to-sum commands

### Unchanged
DB, migrations, `CreateQuickSaleAsync`, backups, other pages.

### Verify
`100` → `+` → `50` → Complete → sale total `150`.

---

## 2026-09-10 — Fix Sales.ReceiptNumber UNIQUE constraint

### Symptom
Checkout showed: `Failed to complete sale: UNIQUE constraint failed: Sales.ReceiptNumber`.

### Not the cause
This was **not** a missing database column. `Sales.ReceiptNumber` already exists with a unique index (`[Indexed(Unique = true)]` on `Sale`).

### Root cause
`SaleService.GenerateReceiptNumber()` used an in-memory daily counter (`_dailyCounter`). After an app restart the counter reset to `0`, so the next sale reused `DP-yyyyMMdd-0001` (or earlier numbers) that were already stored.

### Fix
- File: `Services/SaleService.cs`
- Replaced sync in-memory generator with `GenerateReceiptNumberAsync()`
- On first use each UTC day (or after restart), seeds the counter from:

  `SELECT ReceiptNumber FROM Sales WHERE ReceiptNumber LIKE 'DP-yyyyMMdd-%' ORDER BY ReceiptNumber DESC LIMIT 1`

- Then increments under a `SemaphoreSlim` lock
- Both `CreateSaleAsync` and `CreateQuickSaleAsync` use the new generator

### Scope
- No schema/migration changes
- No backup or other page changes

### How to verify
1. Complete a sale (note receipt number, e.g. `DP-20260910-0001`)
2. Force-stop the app
3. Complete another sale the same day
4. Expect next sequence (e.g. `0002`), not a UNIQUE error
