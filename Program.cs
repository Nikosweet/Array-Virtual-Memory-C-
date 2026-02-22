using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Program {
    class Program {
        private class VirtualMemory {

            private string filepath;
            private char type;
            private short bitmapSize;
            private long arrSize;
            private byte headerSize = 2 + 8 + 1;

            private long pageCount;
            private const short PAGE_SIZE = 512;
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



            public VirtualMemory(string filepath, long arrSize = 10000000, char type = 'I', int stringLength = 0) {
                if (arrSize < 1L) throw new ArgumentOutOfRangeException();
                this.filepath = filepath;
                this.arrSize = arrSize;
                if (type == 'I' || type == 'C' || type == 'V') {
                    this.type = type;
                    switch (type) {
                        case 'I': elementMemory = 4; break;
                        case 'C':
                            elementMemory = 4;
                            headerSize += 4;
                            this.stringLength = stringLength;
                            break;
                        case 'V':
                            elementMemory = 8;
                            headerSize += 4;
                            this.stringLength = stringLength;
                            break;
                    }
                }

                else throw new ArrayTypeMismatchException("Array type is incorrect");
                elementsOnPage = (short)(PAGE_SIZE / elementMemory);
                pageCount = arrSize / (elementsOnPage);
                bitmapSize = (short)(elementsOnPage / 8);
                if (File.Exists(filepath + "/swap.bin")) {
                    Console.WriteLine("hello");
                    filestream = new FileStream(filepath + ".bin", FileMode.Open, FileAccess.ReadWrite);
                    binarywriter = new BinaryWriter(filestream, Encoding.UTF32);
                    binaryreader = new BinaryReader(filestream, Encoding.UTF32);
                    if (type == 'V') {
                        stringfilestream = new FileStream(filepath + "strings.bin", FileMode.Open,
                            FileAccess.ReadWrite);
                        binarystringreader = new BinaryReader(stringfilestream, Encoding.UTF32);
                        binarystringwriter = new BinaryWriter(stringfilestream, Encoding.UTF32);
                    }
                }
                else CreateFile();
            }


            private void CreateFile() { 
                filestream = new FileStream(filepath + ".bin", FileMode.Create, FileAccess.ReadWrite);
                binarywriter = new BinaryWriter(filestream, Encoding.UTF32);
                binaryreader = new BinaryReader(filestream, Encoding.UTF32);
                const byte V = (byte)'V';
                const byte M = (byte)'M';
                binarywriter.Write([V, M]);
                binarywriter.Write(arrSize);
                binarywriter.Write((byte)type);
                switch (type) {
                    case 'C': binarywriter.Write(stringLength); break;
                    case 'V':
                        binarywriter.Write(stringLength);
                        stringfilestream = new FileStream(filepath + "strings.bin", FileMode.Create,
                            FileAccess.ReadWrite);
                        binarystringreader = new BinaryReader(stringfilestream, Encoding.UTF32);
                        binarystringwriter = new BinaryWriter(stringfilestream, Encoding.UTF32);
                        break;
                }

                binarywriter.Flush();
                filestream.Flush();
                for (long i = 0; i < pageCount; i++) {
                    Page page = new Page(i, elementMemory, type);
                    SavePage(page);
                }
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
                foreach (long key in pageCache.Keys) if (key == pageNumber) return pageCache[key];
                
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
                int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()+1;
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
                    case 'I': for (short i = 0; i < elementsOnPage; i++) binarywriter.Write(page.IntData[i]); break;
                    case 'C': for (short i = 0; i < elementsOnPage; i++) binarywriter.Write(page.CharData[i]); break;
                    case 'V': for (int i = 0; i < elementsOnPage; i++) binarywriter.Write(page.LongData[i]); break;
                }
                binarywriter.Flush();
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

                        pageCache[key].IntData[index % elementsOnPage] = value; break;
                    
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
                                    page.Bitmap[byteCharPos] &= (byte)~(1 << bitCharPos);                                }
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
                foreach(long key in pageCache.Keys) SavePage(pageCache[key]);
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
                    Bitmap = new byte[PAGE_SIZE/elementMemory/8];
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
            string filepath = @"/home/nikosweet/Desktop/Code/C# labs/CSharp labs/CSharp labs";
            
        }   
    }
}
