namespace SafarSense;

/// <summary>Tiny helper for coloured console output, so Program.cs stays readable.</summary>
public static class ConsoleUi
{
    public static void Write(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }

    public static void WriteLine(string text, ConsoleColor color) => Write(text + Environment.NewLine, color);

    public static void Banner()
    {
        Console.WriteLine();
        WriteLine("   ____         __            ____                       ", ConsoleColor.DarkYellow);
        WriteLine("  / ___|  __ _ / _| __ _ _ __/ ___|  ___ _ __  ___  ___  ", ConsoleColor.DarkYellow);
        WriteLine("  \\___ \\ / _` | |_ / _` | '__\\___ \\ / _ \\ '_ \\/ __|/ _ \\ ", ConsoleColor.White);
        WriteLine("   ___) | (_| |  _| (_| | |   ___) |  __/ | | \\__ \\  __/ ", ConsoleColor.White);
        WriteLine("  |____/ \\__,_|_|  \\__,_|_|  |____/ \\___|_| |_|___/\\___| ", ConsoleColor.Green);
        Console.WriteLine();
        WriteLine("  SafarSense — Discover India with semantic search + RAG", ConsoleColor.Cyan);
        WriteLine("  .NET 10 + Microsoft.Extensions.AI", ConsoleColor.DarkGray);
        WriteLine("  " + new string('─', 56), ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    public static void Info(string text) => WriteLine(text, ConsoleColor.Green);
    public static void Heading(string text) => WriteLine(text, ConsoleColor.Magenta);
    public static void Error(string text) => WriteLine(text, ConsoleColor.Red);
}
