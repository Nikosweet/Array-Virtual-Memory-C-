namespace Program;

public static class View {
    public static string[] ParseCommand(string input) {
        var result = new List<string>();
        bool inQuotes = false;
        string current = "";

        for (int i = 0; i < input.Length; i++) {
            char c = input[i];

            if (c == '"') {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ' ' && !inQuotes) {
                if (!string.IsNullOrEmpty(current)) {
                    result.Add(current);
                    current = "";
                }
            }
            else {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current)) {
            result.Add(current);
        }

        return result.ToArray();
    }

    public static void HandleHelp() {
        Console.Write(
            "Available commands:\n" +
            "  create filename(type) - creates a file in specified directory\n" +
            "    type formats:\n" +
            "      int - integer array\n" +
            "      char(n) - fixed-length string array\n" +
            "      varchar(max) - variable-length string array\n" +
            "  open filename - opens a file to work with it\n" +
            "  input(index, value) - input a value to specified index\n" +
            "    Note: string values must be in quotes\n" +
            "  print(index) - print value at specified index\n" +
            "  help - show this help\n" +
            "  exit - save files and close program\n\n"
        );
    }

    public static void HandleCreate(string[] parts) {
        if (parts.Length < 2)
            throw new ArgumentException("create command requires filename with type");

        string fullArg = parts[1];
        
        int openParen = fullArg.LastIndexOf('(');
        int closeParen = fullArg.LastIndexOf(')');

        if (openParen == -1 || closeParen == -1)
            throw new ArgumentException("Invalid format. Use: filename(type)");

        string filePath = fullArg.Substring(0, openParen).Trim();
        string typePart = fullArg.Substring(openParen + 1, closeParen - openParen - 1).Trim();
        
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine($"Created directory: {directory}");
        }

        Console.WriteLine($"Creating file: {filePath}");
        
        if (typePart.StartsWith("int")) {
            Console.WriteLine($"Creating int array file: {filePath}");
            VirtualMemory.CreateFile(filePath);
        }
        else if (typePart.StartsWith("char")) {
            int lenParen = typePart.IndexOf('(');
            int lenCloseParen = typePart.LastIndexOf(')');
            if (lenParen != -1 && lenCloseParen != -1) {
                string lengthStr = typePart.Substring(lenParen + 1, lenCloseParen - lenParen - 1);
                if (int.TryParse(lengthStr, out int length)) {
                    Console.WriteLine($"Creating char({length}) array file: {filePath}");
                    VirtualMemory.CreateFile(filePath, 'C', length);
                }
            }
        }
        else if (typePart.StartsWith("varchar")) {
            int lenParen = typePart.IndexOf('(');
            int lenCloseParen = typePart.LastIndexOf(')');
            if (lenParen != -1 && lenCloseParen != -1) {
                string maxLengthStr = typePart.Substring(lenParen + 1, lenCloseParen - lenParen - 1);
                if (int.TryParse(maxLengthStr, out int maxLength)) {
                    Console.WriteLine($"Creating varchar({maxLength}) array file: {filePath}");
                    VirtualMemory.CreateFile(filePath, 'V', maxLength);
                }
            }
        }
        else throw new ArgumentException($"Unknown type: {typePart}");
    }

    public static VirtualMemory HandleOpen(string[] parts) {
        if (parts.Length < 2)
            throw new ArgumentException("open command requires filename");

        string filename = parts[1];
        
        if (!File.Exists(filename + ".bin"))
            throw new FileNotFoundException($"File {filename}.bin not found");
        
        Console.WriteLine($"Opening file: {filename}");
        return new VirtualMemory(filename);
    }

    public static void HandleInput(string[] parts, VirtualMemory file) { 
        if (file == null)
            throw new InvalidOperationException("No file is open. Use 'open' command first.");
                
        if (parts.Length < 2)
            throw new ArgumentException("input command requires (index, value)");

        string argsString = string.Join(" ", parts.Skip(1));
            
        argsString = argsString.Trim();
        
        int openParenIndex = argsString.IndexOf('(');
        int closeParenIndex = argsString.LastIndexOf(')');
        
        if (openParenIndex == -1 || closeParenIndex == -1)
            throw new ArgumentException("Invalid format. Use: input(index, value) or input (index, value)");
        
        string innerContent = argsString.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
        
        int commaIndex = -1;
        bool inQuotes = false;
        for (int i = 0; i < innerContent.Length; i++) {
            if (innerContent[i] == '"') {
                inQuotes = !inQuotes;
            }
            else if (innerContent[i] == ',' && !inQuotes) {
                commaIndex = i;
                break;
            } 
        }
            
        if (commaIndex == -1)
            throw new ArgumentException("Missing comma between index and value");
        
        string indexStr = innerContent.Substring(0, commaIndex).Trim();
        string valueStr = innerContent.Substring(commaIndex + 1).Trim();
        
        if (!int.TryParse(indexStr, out int index))
            throw new ArgumentException($"Invalid index: '{indexStr}'");
        
        if ((valueStr.StartsWith("\"") && valueStr.EndsWith("\""))) 
            valueStr = valueStr.Substring(1, valueStr.Length - 2);
        
        
        Console.WriteLine($"Input at index {index}: {valueStr}");
        
        if (file.type == 'I') {
            if (int.TryParse(valueStr, out int intValue)) file[index] = intValue;
            else throw new ArgumentException($"Value '{valueStr}' is not a valid integer");
        }
        else file[index] = valueStr;
    }

    public static void HandlePrint(string[] parts, VirtualMemory file) {
        if (file == null)
            throw new InvalidOperationException("No file is open. Use 'open' command first.");
            
        if (parts.Length < 2)
            throw new ArgumentException("print command requires index");

        string argsString = string.Join(" ", parts.Skip(1));
        
        int openParenIndex = argsString.IndexOf('(');
        int closeParenIndex = argsString.LastIndexOf(')');
        
        if (openParenIndex != -1 && closeParenIndex != -1) {
            string indexStr = argsString.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
            if (!int.TryParse(indexStr, out int index))
                throw new ArgumentException($"Invalid index: '{indexStr}'");
            
            Console.WriteLine($"Printing value at index {index}");
            Console.WriteLine(file[index]);
        }
        else {
            if (!int.TryParse(argsString, out int index))
                throw new ArgumentException($"Invalid index: '{argsString}'");
            
            Console.WriteLine($"Printing value at index {index}");
            Console.WriteLine(file[index]);
        }
    }

    public static void HandleExit(VirtualMemory? file) {
        Console.WriteLine("Closing file and exiting...");
        file?.Close();
    }
}