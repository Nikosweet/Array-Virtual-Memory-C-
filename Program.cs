using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;

namespace Program {
    class Program {
        private class VirtualMemory {

            private string filepath;
            public char type { get; }
            private short bitmapSize;
            private long arrSize;
            private byte headerSize;

            private long pageCount;
            private static short PAGE_SIZE = 512;
            private byte elementMemory;
            private short elementsOnPage;
            private int stringLength;

            private FileStream filestream;
            private BinaryWriter binarywriter;
            private BinaryReader binaryreader;
            private FileStream? stringfilestream;
            private BinaryWriter? binarystringwriter;
            private BinaryReader? binarystringreader;

            private Dictionary<long, Page> pageCache = new Dictionary<long, Page>();
            private const int MAX_CACHE_SIZE = 30;
            
            public VirtualMemory(string filepath) {
                if (File.Exists(filepath + ".bin")) {
                    this.filepath = filepath;
                    filestream = new FileStream(filepath + ".bin", FileMode.Open, FileAccess.ReadWrite);
                    binarywriter = new BinaryWriter(filestream, Encoding.UTF32);
                    binaryreader = new BinaryReader(filestream, Encoding.UTF32);
                    byte Vbyte = binaryreader.ReadByte();
                    byte Mbyte = binaryreader.ReadByte();
                    if ((char)Vbyte != 'V' || (char)Mbyte != 'M') throw new FileLoadException("That file is not virtual memory type");
                    arrSize = binaryreader.ReadInt64();
                    type = (char)binaryreader.ReadByte();
                    
                    if (type == 'I' || type == 'C' || type == 'V') {
                        switch (type) {
                            case 'I': elementMemory = 4; headerSize = 11; break;
                            case 'C':
                                elementMemory = 4;
                                headerSize = 11 + 4;
                                stringLength = binaryreader.ReadInt32();
                                break;
                            case 'V':
                                elementMemory = 8;
                                headerSize = 11 + 4;
                                stringLength = binaryreader.ReadInt32();
                                stringfilestream = new FileStream(filepath + "strings.bin", FileMode.Open, FileAccess.ReadWrite);
                                binarystringreader = new BinaryReader(stringfilestream, Encoding.UTF32);
                                binarystringwriter = new BinaryWriter(stringfilestream, Encoding.UTF32);
                                break;
                        }
                    }

                    else throw new FileLoadException("Array type is incorrect");

                    elementsOnPage = (short)(PAGE_SIZE / elementMemory);
                    pageCount = arrSize / elementsOnPage;
                    bitmapSize = (short)(elementsOnPage / 8);
                } 
                else throw new FileLoadException("File does not exist");
            }


            public static void CreateFile(string filepath, char type = 'I', int stringLength = 0) {
                if (type != 'I' && type != 'C' && type != 'V') throw new FileLoadException("Array type is incorrect!");
                FileStream filestream = new FileStream(filepath + ".bin", FileMode.Create, FileAccess.Write);
                BinaryWriter binarywriter = new BinaryWriter(filestream, Encoding.UTF32);
                long arrSize = 10000000;
                const byte V = (byte)'V';
                const byte M = (byte)'M';
                binarywriter.Write([V, M]);
                binarywriter.Write(arrSize);
                binarywriter.Write((byte)type);
                byte elementMemory = 0;
                switch (type) {
                    case 'I':
                        elementMemory = 4;
                        break;
                    case 'C': 
                        binarywriter.Write(stringLength);
                        elementMemory = 4;
                        break;
                    case 'V':
                        binarywriter.Write(stringLength);
                        elementMemory = 8;
                        FileStream stringfilestream = new FileStream(filepath + "strings.bin", FileMode.Create, FileAccess.ReadWrite);
                        stringfilestream.Close();
                        break;
                }
                
                short elementsOnPage = (short)(PAGE_SIZE / elementMemory);
                long pageCount = arrSize / elementsOnPage;
                for (long i = 0; i < pageCount; i++) {
                    Page page = new Page(i, elementMemory, type);
                    foreach (byte j in page.Bitmap) binarywriter.Write(j);
                    switch (type) {
                        case 'I':
                            foreach (int j in page.IntData) binarywriter.Write(j);
                            break;
                        case 'C':
                            foreach (char j in page.CharData) binarywriter.Write(j);
                            break;
                        case 'V':
                            foreach (long j in page.LongData) binarywriter.Write(j);
                            break;
                    }
                }
                
                binarywriter.Close();
                filestream.Close();
            }

            private string LoadString(long reference) {
                binarystringwriter.BaseStream.Seek(reference, SeekOrigin.Begin);
                int strLength = binarystringreader.ReadInt32();
                char[] str = new char[strLength];
                for (int i = 0; i < strLength; i++) str[i] = binarystringreader.ReadChar();

                return new string(str);
            }

            private long WriteString(string str) {
                long reference = stringfilestream.Length;
                Console.WriteLine($"reference: {reference}, str.Length: {str.Length}, str: {str}");
                binarystringwriter.BaseStream.Seek(reference, SeekOrigin.Begin);
                binarystringwriter.Write(str.Length);
                foreach (char i in str) binarystringwriter.Write(i);
                return reference;
            }

            private Page LoadPage(long pageNumber) {
                foreach (long key in pageCache.Keys)
                    if (key == pageNumber)
                        return pageCache[key];

                binarywriter.BaseStream.Seek(headerSize + pageNumber * (PAGE_SIZE + bitmapSize), SeekOrigin.Begin);
                byte[] bitmap = binaryreader.ReadBytes(bitmapSize);
                switch (type) {
                    case 'I':
                        int[] intData = new int[elementsOnPage];
                        for (int i = 0; i < elementsOnPage; i++)
                            intData[i] = binaryreader.ReadInt32();
                        Page intPage = new Page(pageNumber, bitmap, intData);
                        if (pageCache.Count == MAX_CACHE_SIZE) RemoveOldPage();
                        pageCache.Add(intPage.PageNumber, intPage);
                        return intPage;
                    case 'C':
                        char[] charData = new char[elementsOnPage];
                        for (int i = 0; i < elementsOnPage; i++)
                            charData[i] = binaryreader.ReadChar();
                        Page charPage = new Page(pageNumber, bitmap, charData);
                        if (pageCache.Count == MAX_CACHE_SIZE) RemoveOldPage();
                        pageCache.Add(charPage.PageNumber, charPage);
                        return charPage;
                    case 'V':
                        long[] longData = new long[elementsOnPage];
                        for (int i = 0; i < elementsOnPage; i++)
                            longData[i] = binaryreader.ReadInt64();
                        Page longPage = new Page(pageNumber, bitmap, longData);
                        if (pageCache.Count == MAX_CACHE_SIZE) RemoveOldPage();
                        pageCache.Add(longPage.PageNumber, longPage);
                        return longPage;
                }

                throw new ArrayTypeMismatchException("Array type is incorrect");
            }

            private void RemoveOldPage() {
                int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1;
                long timeKey = 0;

                foreach (long key in pageCache.Keys)
                    if (pageCache[key].Time < time) {
                        time = pageCache[key].Time;
                        timeKey = key;
                    }

                if (pageCache[timeKey].Flag == 1) SavePage(pageCache[timeKey]);

                pageCache.Remove(timeKey);
            }

            private void SavePage(Page page) {

                long pageNumber = page.PageNumber;

                binarywriter.BaseStream.Seek(headerSize + pageNumber * (PAGE_SIZE + bitmapSize), SeekOrigin.Begin);
                for (short i = 0; i < bitmapSize; i++) binarywriter.Write(page.Bitmap[i]);
                switch (type) {
                    case 'I':
                        for (short i = 0; i < elementsOnPage; i++) binarywriter.Write(page.IntData[i]);
                        break;
                    case 'C':
                        for (short i = 0; i < elementsOnPage; i++) binarywriter.Write(page.CharData[i]);
                        break;
                    case 'V':
                        for (int i = 0; i < elementsOnPage; i++) binarywriter.Write(page.LongData[i]);
                        break;
                }
            }

            private void SetValue(long key, long index, dynamic value) {
                switch (type) {
                    case 'I':
                        pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        byte bytePos = (byte)(index % elementsOnPage / 8);
                        byte bitPos = (byte)(index % elementsOnPage % 8);
                        pageCache[key].Bitmap[bytePos] |= (byte)(1 << bitPos);
                        if (pageCache[key].IntData[index % 128] == value) return;
                        pageCache[key].Flag = 1;

                        pageCache[key].IntData[index % elementsOnPage] = value;
                        break;

                    case 'C':
                        long totalElements = index * stringLength;
                        long startPage = totalElements / elementsOnPage;
                        int startOffset = (int)(totalElements % elementsOnPage);

                        long endElement = (index + 1) * stringLength - 1;
                        long endPage = endElement / elementsOnPage;
                        int endOffset = (int)(endElement % elementsOnPage);

                        int count = 0;
                        int totalPages = (int)(endPage - startPage + 1);
                        for (int i = 0; i < totalPages; i++) {
                            Page page = LoadPage(startPage + i);
                            page.Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            page.Flag = 1;
                            int startJ = (i == 0) ? startOffset : 0;
                            int endJ = (i == totalPages - 1) ? endOffset : elementsOnPage - 1;

                            for (int j = startJ; j <= endJ; j++) {
                                if (count < value.Length) {
                                    page.CharData[j] = value[count++];
                                    byte byteCharPos = (byte)(j / 8);
                                    byte bitCharPos = (byte)(j % 8);
                                    page.Bitmap[byteCharPos] |= (byte)(1 << bitCharPos);
                                }

                                else {
                                    page.CharData[j] = '\0';
                                    byte byteCharPos = (byte)(j / 8);
                                    byte bitCharPos = (byte)(j % 8);
                                    page.Bitmap[byteCharPos] &= (byte)~(1 << bitCharPos);
                                }
                            }
                        }

                        break;

                    case 'V':
                        if (value.Length > stringLength) throw new ValidationException("String is too long");
                        pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        byte byteVarcharPos = (byte)(index % elementsOnPage / 8);
                        byte bitVarcharPos = (byte)(index % elementsOnPage % 8);
                        pageCache[key].Bitmap[byteVarcharPos] |= (byte)(1 << bitVarcharPos);
                        long newReference = WriteString(value);
                        pageCache[key].LongData[index % elementsOnPage] = newReference;
                        pageCache[key].Flag = 1;
                        break;
                }
            }


            public dynamic this[long index] {
                get {
                    if (index >= arrSize || index < 0L) throw new ArgumentOutOfRangeException();
                    switch (type) {
                        case 'I':
                            long pageNumber = index / (byte)(elementsOnPage);
                            foreach (long key in pageCache.Keys)
                                if (key == pageNumber) {
                                    pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    return pageCache[key].IntData[index % elementsOnPage];
                                }

                            Page page = LoadPage(pageNumber) ?? throw new InvalidDataException("Internal data error");
                            return page.IntData[index % elementsOnPage];
                        case 'C':
                            long totalElements = index * stringLength;
                            long startPage = totalElements / elementsOnPage;
                            int startOffset = (int)(totalElements % elementsOnPage);

                            long endElement = (index + 1) * stringLength - 1;
                            long endPage = endElement / elementsOnPage;
                            int endOffset = (int)(endElement % elementsOnPage);

                            int count = 0;
                            int totalPages = (int)(endPage - startPage + 1);
                            char[] str = new char[stringLength];
                            for (int i = 0; i < totalPages; i++) {
                                Page charPage = LoadPage(startPage + i);
                                charPage.Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                int startJ = (i == 0) ? startOffset : 0;
                                int endJ = (i == totalPages - 1) ? endOffset : elementsOnPage - 1;

                                for (int j = startJ; j <= endJ; j++) {
                                    if (charPage.CharData[j] == '\0') return new string(str);
                                    str[count++] = charPage.CharData[j];

                                }
                            }

                            return new string(str);

                        case 'V':
                            long varcharPageNumber = index / (byte)elementsOnPage;
                            foreach (long key in pageCache.Keys)
                                if (key == varcharPageNumber) {
                                    pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    return LoadString(pageCache[key].LongData[index % elementsOnPage]);
                                }

                            Page varcharPage = LoadPage(varcharPageNumber) ??
                                               throw new InvalidDataException("Internal data error");
                            return LoadString(varcharPage.LongData[index % elementsOnPage]);
                    }

                    throw new ArrayTypeMismatchException("Array type is incorrect");
                }

                set {
                    if (index >= arrSize || index < 0) throw new ArgumentOutOfRangeException();
                    long pageNumber = index / elementsOnPage;
                    foreach (long key in pageCache.Keys)
                        if (key == pageNumber) {
                            SetValue(pageNumber, index, value);
                            return;
                        }

                    Page? page = LoadPage(pageNumber);
                    if (page == null) throw new InvalidDataException("Internal data error");
                    SetValue(pageNumber, index, value);
                }
            }

            public void Close() {
                foreach (long key in pageCache.Keys) SavePage(pageCache[key]);
                binarywriter.Close();
                binaryreader.Close();
                filestream.Close();
                if (stringfilestream != null) {
                    binarystringreader.Close();
                    binarystringwriter.Close();
                    stringfilestream.Close();
                }
            }

            private class Page {
                public readonly long PageNumber;
                public byte Flag;
                public int Time;
                public readonly byte[] Bitmap;

                public int[]? IntData;
                public char[]? CharData;
                public long[]? LongData;

                public Page(long pageNumber, byte elementMemory, char type) {
                    PageNumber = pageNumber;
                    Flag = 0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = new byte[PAGE_SIZE / elementMemory / 8];
                    switch (type) {
                        case 'I': IntData = new int[128]; break;
                        case 'C': CharData = new char[128]; break;
                        case 'V': LongData = new long[64]; break;
                    }
                }

                public Page(long pageNumber, byte[] bitmap, int[] data) {
                    PageNumber = pageNumber;
                    Flag = 0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = bitmap;
                    IntData = data;
                }

                public Page(long pageNumber, byte[] bitmap, char[] data) {
                    PageNumber = pageNumber;
                    Flag = 0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = bitmap;
                    CharData = data;
                }

                public Page(long pageNumber, byte[] bitmap, long[] data) {
                    PageNumber = pageNumber;
                    Flag = 0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = bitmap;
                    LongData = data;
                }
            }
        }

        static void Main() {
            VirtualMemory? file = null;
            while (true) {
                Console.Write("VM> ");
                string input = Console.ReadLine().Trim();

                if (string.IsNullOrEmpty(input)) continue;

                string[] parts = ParseCommand(input);
                string command = parts[0].ToLower();

                try {
                    switch (command) {
                        case "help":
                            HandleHelp();
                            break;

                        case "create":
                            HandleCreate(parts);
                            break;

                        case "open":
                            file = HandleOpen(parts);
                            break;

                        case "input":
                            HandleInput(parts, file);
                            break;

                        case "print":
                            HandlePrint(parts, file);
                            break;

                        case "exit":
                            HandleExit(file);
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

        static string[] ParseCommand(string input) {
            var result = new List<string>();
            bool inQuotes = false;
            string current = "";
            
            for (int i = 0; i < input.Length; i++) {
                char c = input[i];
                
                if (c == '"') {
                    inQuotes = !inQuotes;
                    current += c;
                }
                else if (c == ' ' && !inQuotes) {
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

        static void HandleHelp() {
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

        static void HandleCreate(string[] parts) {
            if (parts.Length < 2)
                throw new ArgumentException("create command requires filename with type");

            string fullArg = parts[1];

            int openParen = fullArg.IndexOf('(');
            int closeParen = fullArg.LastIndexOf(')');

            if (openParen == -1 || closeParen == -1)
                throw new ArgumentException("Invalid format. Use: filename(type)");

            string filename = fullArg.Substring(0, openParen);
            string typePart = fullArg.Substring(openParen + 1, closeParen - openParen - 1);

            if (typePart.StartsWith("int")) {
                Console.WriteLine($"Creating int array file: {filename}");
                VirtualMemory.CreateFile(filename);
            }
            else if (typePart.StartsWith("char")) {
                int lenParen = typePart.IndexOf('(');
                int lenCloseParen = typePart.LastIndexOf(')');
                if (lenParen != -1 && lenCloseParen != -1) {
                    string lengthStr = typePart.Substring(lenParen + 1, lenCloseParen - lenParen - 1);
                    if (int.TryParse(lengthStr, out int length)) {
                        Console.WriteLine($"Creating char({length}) array file: {filename}");
                        VirtualMemory.CreateFile(filename, 'C', length);
                    }
                }
            }
            else if (typePart.StartsWith("varchar")) {
                int lenParen = typePart.IndexOf('(');
                int lenCloseParen = typePart.LastIndexOf(')');
                if (lenParen != -1 && lenCloseParen != -1) {
                    string maxLengthStr = typePart.Substring(lenParen + 1, lenCloseParen - lenParen - 1);
                    if (int.TryParse(maxLengthStr, out int maxLength)) {
                        Console.WriteLine($"Creating varchar({maxLength}) array file: {filename}");
                        VirtualMemory.CreateFile(filename, 'V', maxLength);
                    }
                }
            }
            else throw new ArgumentException($"Unknown type: {typePart}");
        }

        static VirtualMemory HandleOpen(string[] parts) {
            if (parts.Length < 2)
                throw new ArgumentException("open command requires filename");

            string filename = parts[1];
            Console.WriteLine($"Opening file: {filename}");
            return new VirtualMemory(filename);
        }

        static void HandleInput(string[] parts, VirtualMemory file) {
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
            
            // Извлекаем индекс и значение
            string indexStr = innerContent.Substring(0, commaIndex).Trim();
            string valueStr = innerContent.Substring(commaIndex + 1).Trim();
            
            if (!int.TryParse(indexStr, out int index))
                throw new ArgumentException($"Invalid index: '{indexStr}'");
            
            if ((valueStr.StartsWith("\"") && valueStr.EndsWith("\""))) {
                valueStr = valueStr.Substring(1, valueStr.Length - 2);
            }
            
            Console.WriteLine($"Input at index {index}: {valueStr}");
            
            if (file.type == 'I') {
                if (int.TryParse(valueStr, out int intValue)) {
                    file[index] = intValue;
                }
                else {
                    throw new ArgumentException($"Value '{valueStr}' is not a valid integer");
                }
            }
            else {
                file[index] = valueStr;
            }
        }

        static void HandlePrint(string[] parts, VirtualMemory file) {
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

        static void HandleExit(VirtualMemory? file) {
            Console.WriteLine("Closing file and exiting...");
            file?.Close();
        }
    }
}