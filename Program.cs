using System.IO;

namespace Program {
    class Program {
        private class VirtualMemory {
            
            private string filepath;
            private char type;
            private short bitmapSize;
            private long arrSize;
            private byte headerSize;
            
            private long pageCount;
            private const short PAGE_SIZE = 512;
            private byte elementMemory;
            
            private FileStream filestream;
            private BinaryWriter binarywriter;
            private BinaryReader binaryreader;


            private Dictionary<long, Page> pageCache = new Dictionary<long, Page>();
            private const int MAX_CACHE_SIZE = 1;

            
            
            public VirtualMemory(string filepath, long arrSize = 10000, char type = 'I') {
                if (arrSize < 1L) throw new ArgumentOutOfRangeException("Array size is incorrect");
                this.filepath = filepath;
                this.arrSize = arrSize;
                if (type == 'I' || type == 'C' || type == 'V') {
                    this.type = type;
                    switch (type) {
                        case 'I': elementMemory = 4; break;
                        case 'C': elementMemory = 1; break;
                        case 'V': elementMemory = 8; break;
                    }
                }
                
                else throw new ArgumentException("Array type is incorrect");
                
                headerSize = 2 + 8 + 1;
                pageCount = arrSize / (PAGE_SIZE / elementMemory);
                bitmapSize = (short)(PAGE_SIZE / elementMemory / 8);
                if (File.Exists(filepath)) {
                    filestream = new FileStream(filepath, FileMode.Open, FileAccess.ReadWrite);
                    binarywriter = new BinaryWriter(filestream, System.Text.Encoding.ASCII);
                    binaryreader = new BinaryReader(filestream, System.Text.Encoding.ASCII);
                }
                else CreateFile(filepath);
            }
            
            
            private void CreateFile(string filepath) {
                filestream = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite);
                binarywriter = new BinaryWriter(filestream, System.Text.Encoding.ASCII);
                binaryreader = new BinaryReader(filestream, System.Text.Encoding.ASCII);
                binarywriter.Write(['V', 'M']);
                binarywriter.Write(arrSize);
                binarywriter.Write(type);
                binarywriter.Flush();
                filestream.Flush();
                for (long i = 0; i < pageCount; i++) {
                    Page page = new Page(i, elementMemory);
                    SavePage(page);
                }
            }
            
            
            private Page? LoadPage(long pageNumber) {
                foreach (long key in pageCache.Keys) if (key == pageNumber) return pageCache[key];
                
                binarywriter.BaseStream.Seek(headerSize + pageNumber * (512L + 16L), SeekOrigin.Begin);
                byte[] bitmap = binaryreader.ReadBytes(bitmapSize);
                
                int[] data = new int[128];
                for (int i = 0; i < 128; i++) 
                    data[i] = binaryreader.ReadInt32();
                Page page = new Page(pageNumber, bitmap, data, elementMemory);
                
                if (pageCache.Count == MAX_CACHE_SIZE) RemoveOldPage();
                
                pageCache.Add(page.PageNumber, page);
                
                return page;
            }

            private void RemoveOldPage() {
                int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()+1;
                long timeKey = 0;

                foreach (long key in pageCache.Keys)
                    if (pageCache[key].Time < time) {
                        time = pageCache[key].Time;
                        timeKey = key;
                    }
                
                if (pageCache[timeKey].Flag == (byte)1) SavePage(pageCache[timeKey]);
                
                pageCache.Remove(timeKey);
            }
            
            private void SavePage(Page page) {

                long pageNumber = page.PageNumber;
                
                binarywriter.BaseStream.Seek(headerSize + pageNumber * (512L + 16L), SeekOrigin.Begin);
                for (short i = 0; i < bitmapSize; i++) binarywriter.Write(page.Bitmap[i]);
                for (short i = 0; i < PAGE_SIZE/elementMemory; i++) binarywriter.Write(page.Data[i]);
                binarywriter.Flush();
            }

            private void SetValue(long key, long index, int value) {
                pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (pageCache[key].Data[index % 128] == value) return;
                            
                byte bytePos = (byte)(index % 128 / 8);
                byte bitPos = (byte)(index % 128 % 8);
                pageCache[key].Bitmap[bytePos] |= (byte)(1 << bitPos);
                            
                pageCache[key].Data[index % 128] = value;
                pageCache[key].Flag = (byte)1;
            }
            
            
            public int this[long index] {
                get {
                    if (index >= arrSize || index < 0L) throw new ArgumentOutOfRangeException();
                    long pageNumber = index / (byte)128;
                    foreach (long key in pageCache.Keys)
                        if (key == pageNumber) {
                            pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            return pageCache[key].Data[index % 128];
                        }
                    
                    Page page = LoadPage(pageNumber) ?? throw new InvalidDataException("Internal data error");
                    return page.Data[index % 128];

                }
                
                set {
                    if (index >= arrSize || index < 0) throw new ArgumentOutOfRangeException();
                    long pageNumber = index / 128;
                    foreach (long key in pageCache.Keys)
                        if (key == pageNumber) {
                            SetValue(key, index, value);
                            return;
                        }
                    
                    Page? page = LoadPage(pageNumber);
                    if (page == null) throw new InvalidDataException("Internal data error");
                    
                    SetValue(pageNumber, index, value);
                }
            }
            public void Close() {
                foreach(int key in pageCache.Keys) SavePage(pageCache[key]);
                binarywriter.Close();
                binaryreader.Close();
                filestream.Close();
            }
            
            private class Page {
                public readonly long PageNumber;
                public byte Flag;
                public int Time;
                public readonly byte[] Bitmap;
                public readonly int[] Data;

                public Page(long pageNumber, byte elementMemory) {
                    PageNumber = pageNumber;
                    Flag = (byte)0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = new byte[PAGE_SIZE/elementMemory/8];
                    Data = new int[128];
                }
                
                public Page(long pageNumber, byte[] bitmap, int[] data, byte elementMemory) {
                    PageNumber = pageNumber;
                    Flag = (byte)0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = bitmap;
                    Data = data;
                }
            }
        }
        
        
        static void Main(string[] args) {
            string filepath = @"/home/nikosweet/Desktop/Code/C# labs/CSharp labs/CSharp labs/swap.bin";
            VirtualMemory MyFile = new VirtualMemory(filepath);
            MyFile[127] = Int32.MaxValue;
            MyFile[128] = Int32.MaxValue; 
            MyFile[0] = Int32.MaxValue;
            MyFile[255] = Int32.MaxValue;
            MyFile[256] = Int32.MaxValue;
            for (int i = 0; i < 4000; i+=2) {
                MyFile[i] = Int32.MaxValue;
            }
            Console.Write(MyFile[0]); 
            Console.Write(MyFile[127]);
            Console.Write(MyFile[128]);
            Console.Write(MyFile[257]);
            MyFile.Close();
        }   
    }
}
