namespace Lesson5PROCESS_HW_;

internal static class Utils
{

    public static bool IsInt(string input) => int.TryParse(input, out _);

    public static void Stoper() { Console.Write("Press any key to continue..."); Console.ReadKey(); }
}