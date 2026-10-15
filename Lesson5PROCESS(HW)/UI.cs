using static System.Console;

namespace Lesson5PROCESS_HW_;

internal static class UI
{
    public static void ShowMainMenu()
    {
        while (true)
        {
            Clear();
            WriteLine("===MAIN MENU===");
            WriteLine("1. Task 1");
            WriteLine("2. Task 2");
            WriteLine("3. Task 3");
            WriteLine("4. Task 4");
            WriteLine("5. Task 5");
            WriteLine("0. Exit");
            Write("Select an option: ");

            switch (ReadLine())
            {
                case "1": T1.Task1.Run(); break;
                case "2": T2.Task2.Run(); break;
                case "3": T3.Task3.Run(); break;
                case "4": T4.Task4.Run(); break;
                case "5": T5.Task5.Run(); break;
                case "0": WriteLine("Exiting..."); return;
                default: WriteLine("Invalid option. Please try again..."); Utils.Stoper(); break;
            }
        }
    }
}