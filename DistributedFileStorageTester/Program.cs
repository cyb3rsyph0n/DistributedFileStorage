using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Permissions;
using System.Web.Services.Protocols;
using System.Web.Services.Configuration;
using System.Reflection;
using DistributedFileStorageCommon;
using DistributedFileStorageTester.DistributedStorageService;
using System.IO;
using System.Threading;

namespace DistributedFileStorageTester
{
    class Program
    {
        static void Main(string[] args)
        {
            DistributedFileStorageCommon.RemoteStorageService tmpService = new RemoteStorageService(new string[] { "http://qwst-brd-icn01/DFS/DistributedStorage.asmx","http://qwst-brd-icn02/DFS/DistributedStorage.asmx","http://qwst-brd-icn03/DFS/DistributedStorage.asmx" });
            //DistributedFileStorageCommon.RemoteStorageService tmpService = new RemoteStorageService(new string[] { "http://localhost:49196/DistributedStorage.asmx" });

            bool result = tmpService.UploadFile("Hello, World!".ToByteArray(), "/", "Hello, World.txt", true, 2, null);
            result = tmpService.DeleteFile("/MCS_FS_IMAGES/2010/01_20/M2302773/", "3655c17d-2b68-45c9-a83d-d1f8eb668cd4.jpg");
            //byte[] tmpResult = tmpService.DownloadFile("/", "Hello, World.txt");

            foreach (FileInfo tmpFile in new DirectoryInfo(string.Join(" ", args)).GetFiles("*.*", SearchOption.AllDirectories).OrderBy(a => a.Length))
            {
                Console.WriteLine("Putting File: {0}", tmpFile.Name);
                try
                {
                    Console.WriteLine(tmpService.UploadFile(tmpFile.FullName, tmpFile.Directory.FullName.Replace(tmpFile.Directory.Root.FullName, "/"), tmpFile.Name, true, 1, null).ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
            Console.Clear();
        }
    }
}
