using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Program {
    class Program {
        static void Main() {
            VirtualMemory? file = null;
            while (true) {
                Console.Write("VM> ");
                string input = Console.ReadLine().Trim();

                if (string.IsNullOrEmpty(input)) continue;

                string[] parts = View.ParseCommand(input);
                string command = parts[0].ToLower();

                try {
                    switch (command) {
                        case "help":
                            View.HandleHelp();
                            break;

                        case "create":
                            View.HandleCreate(parts);
                            break;

                        case "open":
                            file = View.HandleOpen(parts);
                            break;

                        case "input":
                            View.HandleInput(parts, file);
                            break;

                        case "print":
                            View.HandlePrint(parts, file);
                            break;

                        case "exit":
                            View.HandleExit(file);
                            return;

                        default:
                            Console.WriteLine($"Unknown command: {command}");
                            break;
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}