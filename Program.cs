using System.IO;

namespace Program {
    class Program {
        private class VirtualMemory {
            private const int PAGE_SIZE = 512;
            private string filepath;
            private int arrSize;
            private int pageCount;
            
            private FileStream filestream;
            private BinaryWriter binarywriter;
            private BinaryReader binaryreader;


            private Dictionary<int, Page> pageCache = new Dictionary<int, Page>();
            private const int MAX_CACHE_SIZE = 1;

            
            
            public VirtualMemory(string filepath, int arrSize = 10000) {
                this.filepath = filepath;
                this.arrSize = arrSize;
                pageCount = arrSize / (PAGE_SIZE / 4);
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
                binarywriter.Write(10);
                binarywriter.Write('I');
                binarywriter.Flush();
                filestream.Flush();
                for (int i = 0; i < pageCount; i++) {
                    Page page = new Page(i);
                    SavePage(page);
                }
            }
            
            
            private Page? LoadPage(int pageNumber) {
                foreach (int key in pageCache.Keys) if (key == pageNumber) return pageCache[key];
                
                binarywriter.Seek(7 + pageNumber * (512 + 16), SeekOrigin.Begin);
                byte[] bitmap = binaryreader.ReadBytes(16);
                
                int[] data = new int[128];
                for (int i = 0; i < 128; i++) 
                    data[i] = binaryreader.ReadInt32();
                Page page = new Page(pageNumber, bitmap, data);
                
                if (pageCache.Count == MAX_CACHE_SIZE) RemoveOldPage();
                
                pageCache.Add(page.PageNumber, page);
                
                return page;
            }

            private void RemoveOldPage() {
                int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()+1;
                int timeKey = 0;

                foreach (int key in pageCache.Keys)
                    if (pageCache[key].Time < time) {
                        time = pageCache[key].Time;
                        timeKey = key;
                    }
                
                if (pageCache[timeKey].Flag == (byte)1) SavePage(pageCache[timeKey]);
                
                pageCache.Remove(timeKey);
            }
            
            private void SavePage(Page page) {

                int pageNumber = page.PageNumber;
                
                binarywriter.Seek(7 + pageNumber * (512 + 16), SeekOrigin.Begin);
                for (int i = 0; i < 16; i++) binarywriter.Write(page.Bitmap[i]);
                for (int i = 0; i < 128; i++) binarywriter.Write(page.Data[i]);
                binarywriter.Flush();
            }

            private void SetValue(int key, int index, int value) {
                pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (pageCache[key].Data[index % 128] == value) return;
                            
                int bytePos = index % 128 / 8;
                int bitPos = index % 128 % 8;
                pageCache[key].Bitmap[bytePos] |= (byte)(1 << bitPos);
                            
                pageCache[key].Data[index % 128] = value;
                pageCache[key].Flag = (byte)1;
            }
            
            
            public int this[int index] {
                get {
                    if (index >= arrSize || index < 0) throw new ArgumentOutOfRangeException();
                    int pageNumber = index / 128;
                    foreach (int key in pageCache.Keys)
                        if (key == pageNumber) {
                            pageCache[key].Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            return pageCache[key].Data[index % 128];
                        }
                    
                    Page page = LoadPage(pageNumber) ?? throw new InvalidDataException("Internal data error");
                    return page.Data[index % 128];

                }
                
                set {
                    if (index >= arrSize || index < 0) throw new ArgumentOutOfRangeException();
                    int pageNumber = index / 128;
                    foreach (int key in pageCache.Keys)
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
                public readonly int PageNumber;
                public byte Flag;
                public int Time;
                public readonly byte[] Bitmap;
                public readonly int[] Data;

                public Page(int pageNumber) {
                    PageNumber = pageNumber;
                    Flag = (byte)0;
                    Time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Bitmap = new byte[PAGE_SIZE/4/8];
                    Data = new int[128];
                }
                
                public Page(int pageNumber, byte[] bitmap, int[] data) {
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
