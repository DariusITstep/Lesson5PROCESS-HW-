using System;
using System.Diagnostics;
using System.Threading;
using static System.Console;

namespace Lesson5PROCESS_HW_.T5;

internal static class Task5
{
    public static void Run()
    {
        Clear();

        WriteLine("===== ONLINE STORE WAREHOUSE =====");

        Warehouse warehouse = new Warehouse();

        using CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        Thread[] customers = new Thread[8];

        for (int i = 0; i < customers.Length; i++)
        {
            Customer customer = new Customer(i + 1, warehouse, token);
            customers[i] = new Thread(customer.Run);
            customers[i].Start();
        }

        Supplier supplier = new Supplier(warehouse, token);
        Analyst analyst = new Analyst(warehouse, token);

        Thread supplierThread = new Thread(supplier.Run);
        Thread analystThread = new Thread(analyst.Run);

        supplierThread.Start();
        analystThread.Start();

        Thread.Sleep(10000);

        source.Cancel();
        warehouse.WakeCustomers();

        foreach (Thread customer in customers) customer.Join();

        supplierThread.Join();
        analystThread.Join();

        warehouse.ShowFinalReport();

        Utils.Stoper();
    }
}

internal sealed class Warehouse
{
    private readonly object _locker = new object();

    private readonly int[] _stock = { 20, 20, 20, 20, 20 };
    private readonly int[] _supplied = new int[5];
    private readonly int[] _sold = new int[5];

    private readonly long[] _prices =
    {
        1299, 2499, 999, 3499, 1599
    };

    private long _revenueCents;
    private int _completedOrders;
    private int _cancelledOrders;

    public bool PlaceOrder(int[] quantities, CancellationToken token)
    {
        Stopwatch timer = Stopwatch.StartNew();

        lock (_locker)
        {
            while (!CanFulfill(quantities))
            {
                if (token.IsCancellationRequested) return false;

                int remaining = 2000 - (int)timer.ElapsedMilliseconds;

                if (remaining <= 0)
                {
                    Interlocked.Increment(ref _cancelledOrders);
                    WriteLine("Order cancelled: insufficient stock.");
                    return false;
                }

                Monitor.Wait(_locker, remaining);
            }

            if (token.IsCancellationRequested) return false;

            long orderRevenue = 0;

            for (int i = 0; i < _stock.Length; i++)
            {
                if (quantities[i] == 0) continue;

                _stock[i] -= quantities[i];
                _sold[i] += quantities[i];
                orderRevenue += quantities[i] * _prices[i];
            }

            Interlocked.Add(ref _revenueCents, orderRevenue);
            Interlocked.Increment(ref _completedOrders);

            WriteLine("Order completed.");

            return true;
        }
    }

    private bool CanFulfill(int[] quantities)
    {
        for (int i = 0; i < _stock.Length; i++)
        {
            if (_stock[i] < quantities[i]) return false;
        }

        return true;
    }

    public void Restock(Random random)
    {
        lock (_locker)
        {
            int product = random.Next(_stock.Length);
            int amount = random.Next(10, 16);

            _stock[product] += amount;
            _supplied[product] += amount;

            WriteLine($"Supplier: product {product + 1}, +{amount} units.");

            Monitor.PulseAll(_locker);
        }
    }

    public void WakeCustomers()
    {
        lock (_locker) Monitor.PulseAll(_locker);
    }

    public Snapshot GetSnapshot()
    {
        lock (_locker)
        {
            return new Snapshot(
                (int[])_stock.Clone(),
                (int[])_supplied.Clone(),
                (int[])_sold.Clone(),
                Interlocked.Read(ref _revenueCents),
                Interlocked.CompareExchange(ref _completedOrders, 0, 0),
                Interlocked.CompareExchange(ref _cancelledOrders, 0, 0));
        }
    }

    public void ShowFinalReport()
    {
        Snapshot snapshot = GetSnapshot();

        WriteLine();
        WriteLine("===== FINAL WAREHOUSE REPORT =====");

        for (int i = 0; i < _stock.Length; i++)
        {
            int expected = 20 + snapshot.Supplied[i] - snapshot.Sold[i];

            WriteLine(
                $"Product {i + 1}: initial=20, " +
                $"supplied={snapshot.Supplied[i]}, " +
                $"sold={snapshot.Sold[i]}, " +
                $"stock={snapshot.Stock[i]}, " +
                $"expected={expected}");

            WriteLine($"Invariant: {(expected == snapshot.Stock[i] ? "OK" : "ERROR")}");
        }

        WriteLine($"Completed orders: {snapshot.CompletedOrders}");
        WriteLine($"Cancelled orders: {snapshot.CancelledOrders}");
        WriteLine($"Revenue: {snapshot.RevenueCents / 100.0:F2}");
    }

    internal sealed class Snapshot
    {
        public int[] Stock { get; }
        public int[] Supplied { get; }
        public int[] Sold { get; }
        public long RevenueCents { get; }
        public int CompletedOrders { get; }
        public int CancelledOrders { get; }

        public Snapshot(
            int[] stock,
            int[] supplied,
            int[] sold,
            long revenueCents,
            int completedOrders,
            int cancelledOrders)
        {
            Stock = stock;
            Supplied = supplied;
            Sold = sold;
            RevenueCents = revenueCents;
            CompletedOrders = completedOrders;
            CancelledOrders = cancelledOrders;
        }
    }
}

internal sealed class Customer
{
    private readonly int _id;
    private readonly Warehouse _warehouse;
    private readonly CancellationToken _token;

    public Customer(int id, Warehouse warehouse, CancellationToken token)
    {
        _id = id;
        _warehouse = warehouse;
        _token = token;
    }

    public void Run()
    {
        while (!_token.IsCancellationRequested)
        {
            int[] order = CreateOrder();

            WriteLine($"Customer {_id} places an order.");

            _warehouse.PlaceOrder(order, _token);

            Thread.Sleep(Random.Shared.Next(100, 401));
        }
    }

    private static int[] CreateOrder()
    {
        int[] quantities = new int[5];
        int productCount = Random.Shared.Next(1, 4);

        while (productCount > 0)
        {
            int product = Random.Shared.Next(5);

            if (quantities[product] != 0) continue;

            quantities[product] = Random.Shared.Next(1, 6);
            productCount--;
        }

        return quantities;
    }
}

internal sealed class Supplier
{
    private readonly Warehouse _warehouse;
    private readonly CancellationToken _token;

    public Supplier(Warehouse warehouse, CancellationToken token)
    {
        _warehouse = warehouse;
        _token = token;
    }

    public void Run()
    {
        while (!_token.IsCancellationRequested)
        {
            Thread.Sleep(700);

            if (_token.IsCancellationRequested) break;

            _warehouse.Restock(Random.Shared);
        }
    }
}

internal sealed class Analyst
{
    private readonly Warehouse _warehouse;
    private readonly CancellationToken _token;

    public Analyst(Warehouse warehouse, CancellationToken token)
    {
        _warehouse = warehouse;
        _token = token;
    }

    public void Run()
    {
        while (!_token.IsCancellationRequested)
        {
            Thread.Sleep(500);

            if (_token.IsCancellationRequested) break;

            Warehouse.Snapshot snapshot = _warehouse.GetSnapshot();

            WriteLine(
                $"[ANALYST] Stock: {string.Join(", ", snapshot.Stock)} | " +
                $"Revenue: {snapshot.RevenueCents / 100.0:F2} | " +
                $"Completed: {snapshot.CompletedOrders} | " +
                $"Cancelled: {snapshot.CancelledOrders}");
        }
    }
}