using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using static System.Console;

namespace Lesson5PROCESS_HW_.T3;

internal static class Task3
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object OutputLocker = new object();

    private static int _carsOnBridge;
    private static int _directionChanges;
    private static int _totalBridgeOccupancy;
    private static int _totalCars;

    public static void Run()
    {
        Clear();

        WriteLine("===== ONE-LANE BRIDGE =====");
        WriteLine("1. Basic level");
        WriteLine("2. Fairness");
        WriteLine("3. Bridge capacity: 3");
        Write("Select level: ");

        if (!int.TryParse(ReadLine(), out int level) || level < 1 || level > 3)
        {
            WriteLine("Invalid level.");
            Utils.Stoper();
            return;
        }

        _carsOnBridge = 0;
        _directionChanges = 0;
        _totalBridgeOccupancy = 0;
        _totalCars = 20;

        Bridge bridge = new Bridge(level);

        Thread[] cars = new Thread[_totalCars];

        for (int i = 0; i < cars.Length; i++)
        {
            int id = i + 1;
            int side = Random.Shared.Next(2);

            cars[i] = new Thread(() =>
            {
                Thread.Sleep(Random.Shared.Next(100, 801));

                Car car = new Car(id, side);

                bridge.Enter(car);

                Thread.Sleep(500);

                bridge.Leave(car);
            });

            cars[i].Start();
        }

        foreach (Thread car in cars) car.Join();

        WriteLine();
        WriteLine("===== STATISTICS =====");
        WriteLine($"Cars completed: {_totalCars}");
        WriteLine($"Direction changes: {_directionChanges}");

        if (_totalCars > 0)
        {
            WriteLine($"Average bridge occupancy: " + $"{(double)_totalBridgeOccupancy / _totalCars:F2} cars.");
        }

        bridge.ShowStatistics();

        Utils.Stoper();
    }

    private static void Log(string message)
    {
        lock (OutputLocker)
        {
            TimeSpan time = Clock.Elapsed;

            WriteLine($"[{time:mm\\:ss\\.fff}] {message}");
        }
    }

    private sealed class Car
    {
        public int Id { get; }
        public int Side { get; }

        public string Direction => Side == 0 ? "→" : "←";
        public string Bank => Side == 0 ? "Left bank" : "Right bank";

        public long ArrivalTime { get; }

        public Car(int id, int side)
        {
            Id = id;
            Side = side;
            ArrivalTime = Clock.ElapsedMilliseconds;
        }
    }

    private sealed class Bridge
    {
        private const int Capacity = 3;

        private readonly object _locker = new object();
        private readonly int _level;

        private readonly int[] _waiting = new int[2];

        private readonly List<long>[] _waitTimes = { new List<long>(), new List<long>() };

        private int _direction = -1;
        private int _carsOnBridge;
        private int _consecutiveCars;

        private long _occupancySum;
        private long _occupancySamples;

        public Bridge(int level)
        {
            _level = level;
        }

        public void Enter(Car car)
        {
            lock (_locker)
            {
                _waiting[car.Side]++;

                Log($"Car #{car.Id} ({car.Direction}) waits.");

                while (!CanEnter(car)) Monitor.Wait(_locker);

                _waiting[car.Side]--;

                if (_direction != car.Side)
                {
                    if (_direction != -1) Interlocked.Increment(ref _directionChanges);

                    _direction = car.Side;
                    _consecutiveCars = 0;
                }

                _consecutiveCars++;
                _carsOnBridge++;

                int current = Interlocked.Increment(ref _carsOnBridge);

                if ((_level == 1 && current > 1) || (_level == 3 && current > Capacity))
                {
                    Log("BOOM!");
                }

                _occupancySum += _carsOnBridge;
                _occupancySamples++;

                long waitTime = Clock.ElapsedMilliseconds - car.ArrivalTime;

                _waitTimes[car.Side].Add(waitTime);

                Log($"Car #{car.Id} ({car.Direction}) enters bridge. " + $"On bridge: {_carsOnBridge}.");

                Monitor.PulseAll(_locker);
            }
        }

        private bool CanEnter(Car car)
        {
            if (_level == 1) return _carsOnBridge == 0;

            if (_carsOnBridge == 0)
            {
                if (_direction == -1 || _direction == car.Side) return true;

                return _waiting[1 - car.Side] == 0;
            }

            if (_direction != car.Side) return false;

            if (_level == 2) return false;

            if (_carsOnBridge >= Capacity) return false;

            if (_consecutiveCars >= 5 && _waiting[1 - car.Side] > 0)  return false;

            return true;
        }
        public void Leave(Car car)
        {
            lock (_locker)
            {
                _carsOnBridge--;

                Interlocked.Decrement(ref _carsOnBridge);

                Log($"Car #{car.Id} ({car.Direction}) leaves bridge. " +  $"On bridge: {_carsOnBridge}.");

                if (_carsOnBridge == 0)
                {
                    if (_level == 2 && _waiting[1 - _direction] > 0)
                    {
                        _direction = 1 - _direction;
                        Interlocked.Increment(ref _directionChanges);
                    }

                    if (_level == 3 &&
                        _consecutiveCars >= 5 &&
                        _waiting[1 - _direction] > 0)
                    {
                        _direction = 1 - _direction;
                        _consecutiveCars = 0;

                        Interlocked.Increment(ref _directionChanges);
                    }
                }

                Monitor.PulseAll(_locker);
            }
        }

        public void ShowStatistics()
        {
            lock (_locker)
            {
                for (int side = 0; side < 2; side++)
                {
                    string bank = side == 0 ? "Left bank" : "Right bank";

                    List<long> times = _waitTimes[side];

                    long max = times.Count > 0 ? Max(times) : 0;

                    double average = times.Count > 0 ? Average(times) : 0;

                    WriteLine($"{bank}:");
                    WriteLine($"  Cars: {times.Count}");
                    WriteLine($"  Maximum wait: {max} ms");
                    WriteLine($"  Average wait: {average:F2} ms");
                }

                if (_occupancySamples > 0)
                {
                    WriteLine($"Average occupancy while entering: " + $"{(double)_occupancySum / _occupancySamples:F2}");
                }
            }
        }

        private static long Max(List<long> values)
        {
            long max = values[0];

            foreach (long value in values) if (value > max) max = value;

            return max;
        }

        private static double Average(List<long> values)
        {
            long sum = 0;

            foreach (long value in values) sum += value;

            return (double)sum / values.Count;
        }
    }
}