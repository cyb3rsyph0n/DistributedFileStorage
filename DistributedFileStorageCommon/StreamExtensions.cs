using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.IO.Compression;

namespace DistributedFileStorageCommon
{
    public static class StreamExtensions
    {
        /// <summary>
        /// COMPUTE THE HASH VALUE OF THE FILE STREAM
        /// </summary>
        /// <param name="s">STREAM TO COMPUTE THE VALUE OF</param>
        /// <param name="closeStream">BOOLEAN TO DETERMINE IF THE STREAM SHOULD BE CLOSED UPON EXIT</param>
        /// <returns>RETURNS AN MD5 HASH OF THE FILE</returns>
        public static string ComputeMD5(this Stream s, bool closeStream)
        {
            try
            {
                //SEEK TO THE BEGINING OF THE STREAM
                s.Seek(0, SeekOrigin.Begin);

                //CREATE AN MD5 AND COMPUTE THE HASH VALUE
                return BitConverter.ToString(MD5.Create().ComputeHash(s)).Replace("-", "");
            }
            catch
            {
                //THROW AN ERROR BECAUSE WE WERE UNABLE TO SEEK IN THE FILE OR POSSIBLY READ
                throw new ArgumentException("Invalid stream, must be able to seek and read.");
            }
            finally
            {
                //IF WE ARE SUPPOSED TO CLOSE THE STREAM WHEN WE ARE DONE THEN DO SO
                if (closeStream)
                    s.Close();
            }

        }

        /// <summary>
        /// READS A STREAM AND RETURNS A STRING RESULT
        /// </summary>
        /// <param name="srcStream">INCOMING STREAM TO READ FROM</param>
        /// <returns>STRING REPRESENTATION OF WHAT WAS IN THE STREAM</returns>
        public static string ReadStringFromStream(this Stream srcStream)
        {
            StreamReader tmpReader = new StreamReader(srcStream);
            string tmpReturn = tmpReader.ReadToEnd();
            return tmpReturn;
        }

        /// <summary>
        /// WRITES A STRING TO AN OUTBOUND STREAM
        /// </summary>
        /// <param name="destStream">OUTBOUND STREAM FOR WRITING THE STRING TO</param>
        /// <param name="value">VALUE OF THE STRING TO WRITE TO THE STREAM</param>
        public static void WriteStringToStream(this Stream destStream, string value)
        {
            StreamWriter tmpWriter = new StreamWriter(destStream);
            tmpWriter.WriteLine(value);
            tmpWriter.Flush();
        }

        /// <summary>
        /// COPIES SOURCE STREAM DIRECTLY TO THE DEST STREAM
        /// </summary>
        /// <param name="src">SOURCE STREAM TO READ FROM</param>
        /// <param name="dest">DEST STREAM TO WRITE TO</param>
        public static void Copy(this Stream src, Stream dest)
        {
            int count;
            byte[] buffer = new byte[1024];

            //WHILE WE ARE STILL ABLE TO READ BYTES FROM THE SOURCE WRITE THEM TO THE DEST
            while ((count = src.Read(buffer, 0, buffer.Length)) != 0)
            {
                dest.Write(buffer, 0, count);
            }

            //FLUSH THE STREAM SO ALL DATA IS MOVED INTO THE STREAM
            dest.Flush();
        }

        /// <summary>
        /// READS FROM SOURCE STREAM AND WRITES COMPRESSED VERSION TO THE DEST STREAM
        /// </summary>
        /// <param name="src">SOURCE STREAM TO COMPRESS</param>
        /// <param name="dest">DEST OF COMPRESSED STREAM</param>
        public static void Compress(this Stream src, Stream dest)
        {
            //CREATE A COMPRESSING GZIP STREAM
            using(GZipStream tmpCompressor = new GZipStream(dest, CompressionMode.Compress))
            {
                int count;
                byte[] buffer = new byte[1024];

                //WHILE WE ARE STILL ABLE TO READ BYTES FROM THE SOURCE WRITE THEM TO THE DEST
                while ((count = src.Read(buffer, 0, buffer.Length)) != 0)
                {
                    tmpCompressor.Write(buffer, 0, count);
                }

                //FLUSH THE STREAM SO ALL DATA IS MOVED INTO THE STREAM
                tmpCompressor.Flush();
            }
        }

        /// <summary>
        /// READS COMPRESSED SOURCE STREAM AND WRITES UNCOMPRESSED VERSION TO THE DEST STREAM
        /// </summary>
        /// <param name="src">SOURCE OF COMPRESSED STREAM</param>
        /// <param name="dest">DEST OF UNCOMPRESSED STREAM</param>
        public static void Uncompress(this Stream src, Stream dest)
        {
            //CREATE A DECOMPRESSING GZIP STREAM
            using (GZipStream tmpDecompressor = new GZipStream(src, CompressionMode.Decompress))
            {
                int count;
                byte[] buffer = new byte[1024];

                //WHILE WE ARE STILL ABLE TO READ BYTES FROM THE SOURCE WRITE THEM TO THE DEST
                while ((count = tmpDecompressor.Read(buffer, 0, buffer.Length)) != 0)
                {
                    dest.Write(buffer, 0, count);
                }

                //FLUSH THE STREAM SO ALL DATA IS MOVED INTO THE STREAM
                dest.Flush();
            }
        }
    }
}
