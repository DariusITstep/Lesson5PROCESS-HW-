using static System.Console;

namespace Lesson5PROCESS_HW_.T2;

internal static class Task2
{
    private const int TotalBuns = 60;

    private static int _baked;
    private static int _sold;
    private static int _nextBunId;

    public static void Run()
    {
        Clear();

        _baked = 0;
        _sold = 0;
        _nextBunId = 0;

        Showcase showcase = new Showcase();

        Thread[] bakers = new Thread[3];
        Thread[] customers = new Thread[5];

        for (int i = 0; i < customers.Length; i++)
        {
            int customerId = i + 1;

            customers[i] = new Thread(() => CustomerWork(customerId, showcase, customerId == 5));

            customers[i].Start();
        }

        for (int i = 0; i < bakers.Length; i++)
        {
            int bakerId = i + 1;

            bakers[i] = new Thread(() => BakerWork(bakerId, showcase));

            bakers[i].Start();
        }

        foreach (Thread baker in bakers) baker.Join();

        showcase.Close();

        foreach (Thread customer in customers) customer.Join();

        WriteLine();
        WriteLine("===== BAKERY CLOSED =====");
        WriteLine($"Baked: {_baked}");
        WriteLine($"Sold: {_sold}");

        Utils.Stoper();
    }

    private static void BakerWork(int bakerId, Showcase showcase)
    {
        while (true)
        {
            int number = Interlocked.Increment(ref _nextBunId);

            if (number > TotalBuns) return;

            Thread.Sleep(Random.Shared.Next(200, 601));

            Bun bun = new Bun(number, Random.Shared.Next(3), DateTime.Now);

            showcase.Put(bun, bakerId);

            Interlocked.Increment(ref _baked);
        }
    }

    private static void CustomerWork(int customerId, Showcase showcase, bool impatient)
    {
        while (true)
        {
            Thread.Sleep(Random.Shared.Next(300, 901));

            Bun bun;

            if (impatient)
            {
                if (!showcase.TryTake(500, out bun, customerId))
                {
                    if (showcase.IsClosedAndEmpty) return;

                    WriteLine($"Customer {customerId}: waited 500 ms, left empty-handed.");

                    continue;
                }
            }
            else
            {
                if (!showcase.TryTake(Timeout.Infinite, out bun, customerId)) return;
            }

            Interlocked.Increment(ref _sold);

            double elapsed = (DateTime.Now - bun.PlacedAt).TotalMilliseconds;

            WriteLine($"Customer {customerId} bought bun #{bun.Id} " + $"({bun.Kind}), display time: {elapsed:F0} ms.");
        }
    }

    private sealed class Bun
    {
        public int Id { get; }
        public string Kind { get; }
        public DateTime PlacedAt { get; }

        public Bun(int id, int kind, DateTime placedAt)
        {
            Id = id;

            Kind = kind switch
            {
                0 => "with poppy seeds",
                1 => "with cinnamon",
                _ => "with cherries"
            };

            PlacedAt = placedAt;
        }
    }

    private sealed class Showcase
    {
        private const int Capacity = 5;

        private readonly Queue<Bun> _queue = new Queue<Bun>();
        private readonly object _locker = new object();

        private bool _closed;

        public bool IsClosedAndEmpty
        {
            get
            {
                lock (_locker) return _closed && _queue.Count == 0;
            }
        }

        public void Put(Bun bun, int bakerId)
        {
            lock (_locker)
            {
                while (_queue.Count == Capacity && !_closed)
                {
                    WriteLine($"Baker {bakerId} waits: showcase is full.");

                    Monitor.Wait(_locker);
                }

                if (_closed) return;

                _queue.Enqueue(bun);

                WriteLine($"Baker {bakerId} placed bun #{bun.Id}. " + $"Showcase: {_queue.Count}/{Capacity}.");

                Monitor.PulseAll(_locker);
            }
        }

        public bool TryTake(int timeoutMs, out Bun bun, int customerId)
        {
            lock (_locker)
            {
                DateTime deadline = timeoutMs == Timeout.Infinite ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);

                while (_queue.Count == 0 && !_closed)
                {
                    if (timeoutMs == Timeout.Infinite)
                    {
                        WriteLine($"Customer {customerId} waits: showcase is empty.");

                        Monitor.Wait(_locker);
                    }
                    else
                    {
                        int remaining = (int)((deadline - DateTime.UtcNow).TotalMilliseconds);

                        if (remaining <= 0)
                        {
                            bun = null;
                            return false;
                        }

                        WriteLine($"Customer {customerId} waits up to {remaining} ms.");

                        if (!Monitor.Wait(_locker, remaining))
                        {
                            bun = null;
                            return false;
                        }
                    }
                }

                if (_queue.Count == 0)
                {
                    bun = null;
                    return false;
                }

                bun = _queue.Dequeue();

                WriteLine($"Customer {customerId} took bun #{bun.Id}. " + $"Showcase: {_queue.Count}/{Capacity}.");

                Monitor.PulseAll(_locker);

                return true;
            }
        }

        public void Close()
        {
            lock (_locker)
            {
                _closed = true;
                Monitor.PulseAll(_locker);
            }
        }
    }
}
