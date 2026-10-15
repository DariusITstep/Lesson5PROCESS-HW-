using static System.Console;

namespace Lesson5PROCESS_HW_.T4;

internal static class Task4
{
    private static readonly object OutputLocker = new object();

    public static void Run()
    {
        Clear();

        WriteLine("===== INTERSECTION =====");
        WriteLine("1. Deadlock demonstration");
        WriteLine("2. Ordered quadrants");
        WriteLine("3. TryEnter with backoff");
        WriteLine("4. Traffic controller");
        WriteLine("0. Back");
        Write("Select option: ");

        switch (ReadLine())
        {
            case "1": RunDeadlockDemo(); break;
            case "2": RunOrdered(); break;
            case "3": RunTryEnter(); break;
            case "4": RunController(); break;
            case "0": return;
            default: WriteLine("Invalid option."); break;
        }

        Utils.Stoper();
    }

    private static void Log(string message)
    {
        lock (OutputLocker) WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    private sealed class Car
    {
        public int Id { get; }
        public int EntryQuadrant { get; }
        public int Maneuver { get; }

        public Car(int id, int entryQuadrant, int maneuver)
        {
            Id = id;
            EntryQuadrant = entryQuadrant;
            Maneuver = maneuver;
        }

        public int[] GetQuadrants()
        {
            int count = Maneuver switch
            {
                1 => 1,
                2 => 2,
                _ => 3
            };

            int[] quadrants = new int[count];

            for (int i = 0; i < count; i++) quadrants[i] = (EntryQuadrant + i) % 4;

            return quadrants;
        }
    }

    private static void RunDeadlockDemo()
    {
        object[] quadrants =
        {
            new object(), new object(),
            new object(), new object()
        };

        using CountdownEvent ready = new CountdownEvent(4);
        using ManualResetEventSlim start = new ManualResetEventSlim(false);

        WriteLine("Starting four cars moving straight...");

        for (int i = 0; i < 4; i++)
        {
            int id = i;

            Thread thread = new Thread(() =>
            {
                int first = id;
                int second = (id + 1) % 4;

                lock (quadrants[first])
                {
                    Log($"Car {id + 1} occupies quadrant {first}.");

                    ready.Signal();
                    start.Wait();

                    Log($"Car {id + 1} waits for quadrant {second}.");

                    lock (quadrants[second])
                    {
                        Log($"Car {id + 1} passed.");
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        ready.Wait();
        start.Set();

        Thread.Sleep(500);

        Log("Deadlock demonstrated: each car holds one quadrant " + "and waits for the next.");
    }

    private static void RunOrdered()
    {
        object[] quadrants =
        {
            new object(), new object(),
            new object(), new object()
        };

        Thread[] cars = new Thread[4];

        for (int i = 0; i < cars.Length; i++)
        {
            int id = i;

            cars[i] = new Thread(() =>
            {
                Car car = new Car(id + 1, id, 2);
                int[] required = car.GetQuadrants();

                Array.Sort(required);

                List<object> acquired = new List<object>();

                try
                {
                    foreach (int quadrant in required)
                    {
                        Monitor.Enter(quadrants[quadrant]);
                        acquired.Add(quadrants[quadrant]);

                        Log($"Car {car.Id} acquired quadrant {quadrant}.");
                    }

                    Log($"Car {car.Id} passes the intersection.");
                    Thread.Sleep(500);
                }
                finally
                {
                    for (int j = acquired.Count - 1; j >= 0; j--) Monitor.Exit(acquired[j]);

                    Log($"Car {car.Id} left the intersection.");
                }
            });

            cars[i].Start();
        }

        foreach (Thread car in cars) car.Join();

        Log("Ordered-quadrant demonstration completed.");
    }

    private static void RunTryEnter()
    {
        object[] quadrants =
        {
            new object(), new object(),
            new object(), new object()
        };

        Thread[] cars = new Thread[4];

        for (int i = 0; i < cars.Length; i++)
        {
            int id = i;

            cars[i] = new Thread(() =>
            {
                Car car = new Car(id + 1, id, 2);
                int[] required = car.GetQuadrants();

                while (true)
                {
                    List<object> acquired = new List<object>();
                    bool success = true;

                    foreach (int quadrant in required)
                    {
                        if (Monitor.TryEnter(quadrants[quadrant], 100))
                        {
                            acquired.Add(quadrants[quadrant]);
                        }
                        else
                        {
                            success = false;
                            break;
                        }
                    }

                    if (success)
                    {
                        try
                        {
                            Log($"Car {car.Id} passes the intersection.");
                            Thread.Sleep(500);
                        }
                        finally
                        {
                            for (int j = acquired.Count - 1; j >= 0; j--) Monitor.Exit(acquired[j]);
                        }

                        Log($"Car {car.Id} left the intersection.");
                        break;
                    }

                    for (int j = acquired.Count - 1; j >= 0; j--) Monitor.Exit(acquired[j]);

                    Log($"Car {car.Id} releases quadrants and retries.");

                    Thread.Sleep(Random.Shared.Next(100, 301));
                }
            });

            cars[i].Start();
        }

        foreach (Thread car in cars) car.Join();

        Log("TryEnter demonstration completed.");
    }

    private static void RunController()
    {
        IntersectionController controller = new IntersectionController();

        Thread[] cars = new Thread[8];

        for (int i = 0; i < cars.Length; i++)
        {
            int id = i + 1;
            int entry = (id - 1) % 4;
            int maneuver = Random.Shared.Next(1, 4);

            cars[i] = new Thread(() =>
            {
                Car car = new Car(id, entry, maneuver);

                Log($"Car {car.Id} requests the intersection.");

                controller.Enter(car);

                try
                {
                    Log($"Car {car.Id} passes the intersection.");
                    Thread.Sleep(500);
                }
                finally
                {
                    controller.Leave(car);
                }
            });

            cars[i].Start();
        }

        foreach (Thread car in cars) car.Join();

        Log("Traffic-controller demonstration completed.");
    }

    private sealed class IntersectionController
    {
        private readonly object _locker = new object();
        private readonly bool[] _occupied = new bool[4];

        public void Enter(Car car)
        {
            int[] required = car.GetQuadrants();

            lock (_locker)
            {
                while (!CanReserve(required)) Monitor.Wait(_locker);

                foreach (int quadrant in required) _occupied[quadrant] = true;

                Log($"Controller grants car {car.Id}: " + $"quadrants {string.Join(", ", required)}.");
            }
        }

        private bool CanReserve(int[] required)
        {
            foreach (int quadrant in required)
            {
                if (_occupied[quadrant]) return false;
            }

            return true;
        }

        public void Leave(Car car)
        {
            int[] required = car.GetQuadrants();

            lock (_locker)
            {
                foreach (int quadrant in required) _occupied[quadrant] = false;

                Log($"Car {car.Id} releases the intersection.");

                Monitor.PulseAll(_locker);
            }
        }
    }
}