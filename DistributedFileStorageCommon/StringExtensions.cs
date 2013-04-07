using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace DistributedFileStorageCommon
{
    public static class StringExtensions
    {
        /// <summary>
        /// STRING EXTENSION FOR COMPRESSING TO A BASE 64 STRING
        /// </summary>
        /// <param name="s">INPUT STRING TO COMPRESS</param>
        /// <returns>RETURNS A BASE 64 ENCODED COMPRESSED STRING</returns>
        public static string Compress(this string s)
        {
            //CREATE A TEMP MEMORY STREAM TO HOLD THE COMPRESSED DATA
            using (MemoryStream tmpMem = new MemoryStream())
            {
                //CREATE A GZIP STREAM TO COMPRESS THE INCOMING DATA
                using (GZipStream tmpCompressor = new GZipStream(tmpMem, CompressionMode.Compress))
                {
                    //COMPRESS THE DATA AND CLOSE THE STREAM
                    tmpCompressor.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(s), 0, s.Length);
                    tmpCompressor.Flush();
                    tmpCompressor.Close();
                }

                //ENCODE THE BYTE ARRAY AS A BASE 64 STRING TO REMOVE NULLS AND SUCH
                return Convert.ToBase64String(tmpMem.ToArray());
            }
        }

        /// <summary>
        /// STRING EXTENSION FOR UNCOMPRESSING FROM A BASE 64 STRING
        /// </summary>
        /// <param name="s">BASE 64 STRING WHICH CONTAINS THE COMPRESSED DATA</param>
        /// <returns>RETURN AN UNCOMPRESSED STRING</returns>
        public static string UnCompress(this string s)
        {
            //CREATE A TEMP MEMORY STREAM CONTAINING THE COMPRESSED DATA
            using (MemoryStream tmpMem = new MemoryStream(Convert.FromBase64String(s)))
            {
                //CREATE A GZIP STREAM FOR UNCOMPRESSING THE DATA IN THE STREAM
                using (GZipStream tmpCompressed = new GZipStream(tmpMem, CompressionMode.Decompress))
                {
                    byte[] buffer = new byte[1024];
                    int count = 0;
                    StringBuilder tmpReturn = new StringBuilder();

                    //WHILE WE ARE STILL READING DATA KEEP APPENDING IT TO THE STRING BUILDER
                    while ((count = tmpCompressed.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        tmpReturn.Append(System.Text.ASCIIEncoding.ASCII.GetString(buffer, 0, count));
                    }

                    //RETURN THE RESULT FROM THE STRING BUILDER APPENDING
                    return tmpReturn.ToString();
                }
            }
        }

        public static byte[] ToByteArray(this string s)
        {
            return System.Text.ASCIIEncoding.UTF8.GetBytes(s);
        }
    }
}