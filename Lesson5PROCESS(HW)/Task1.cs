using static System.Console;

namespace Lesson5PROCESS_HW_.T1;

internal static class Task1
{
    private static readonly object _pingPongLocker = new object();
    private static readonly object _relayLocker = new object();

    private static int _turn = 1;
    private static int _currentRunner = 1;

    public static void Run()
    {
        Clear();

        WriteLine("===== WITHOUT COORDINATION =====");

        Thread ping = new Thread(() => RunWithoutOrder("Ping"));
        Thread pong = new Thread(() => RunWithoutOrder("Pong"));

        ping.Start();
        pong.Start();

        ping.Join();
        pong.Join();

        WriteLine();
        WriteLine("===== PING-PONG =====");

        ping = new Thread(() => RunPingPong(1, "Ping"));
        pong = new Thread(() => RunPingPong(2, "Pong"));

        ping.Start();
        pong.Start();

        ping.Join();
        pong.Join();

        WriteLine();
        WriteLine("===== RELAY RACE =====");

        Write("Enter number of runners (3-10): ");

        if (!int.TryParse(ReadLine(), out int runnerCount) ||
            runnerCount < 3 || runnerCount > 10)
        {
            WriteLine("Invalid number of runners.");
            Utils.Stoper();
            return;
        }

        RunRelay(runnerCount);

        Utils.Stoper();
    }

    private static void RunWithoutOrder(string word)
    {
        for (int i = 0; i < 10; i++)
        {
            lock (_pingPongLocker)
            {
                WriteLine(word);
            }
        }
    }

    private static void RunPingPong(int myTurn, string word)
    {
        for (int i = 0; i < 10; i++)
        {
            lock (_pingPongLocker)
            {
                while (_turn != myTurn) Monitor.Wait(_pingPongLocker);

                WriteLine(word);

                _turn = myTurn == 1 ? 2 : 1;

                Monitor.Pulse(_pingPongLocker);
            }
        }
    }

    private static void RunRelay(int runnerCount)
    {
        Thread[] runners = new Thread[runnerCount];
        _currentRunner = 1;

        for (int i = 0; i < runnerCount; i++)
        {
            int runnerId = i + 1;

            runners[i] = new Thread(() => RunRunner(runnerId, runnerCount));

            runners[i].Start();
        }

        foreach (Thread runner in runners) runner.Join();

        WriteLine("Relay finished!");
    }

    private static void RunRunner(int runnerId, int runnerCount)
    {
        for (int lap = 0; lap < 3; lap++)
        {
            lock (_relayLocker)
            {
                while (_currentRunner != runnerId) Monitor.Wait(_relayLocker);

                Write($"Runner {runnerId} -> ");

                if (runnerId == runnerCount) WriteLine();

                _currentRunner = _currentRunner % runnerCount + 1;

                Monitor.PulseAll(_relayLocker);
            }
        }
    }
}