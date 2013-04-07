using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DistributedFileStorageCommon.DistributedStorage;

namespace DistributedFileStorageCommon
{
    public class RemoteStorageService
    {
        private DistributedStorage.DistributedStorage tmpService = new DistributedFileStorageCommon.DistributedStorage.DistributedStorage();

        private string[] mRemoteServiceHandlers = null;
        public string[] RemoteServiceHandlers
        {
            get
            {
                return mRemoteServiceHandlers;
            }
        }

        private int mDefaultBufferSize = 1024 * 1024;
        public int DefaultBufferSize
        {
            get
            {
                return mDefaultBufferSize;
            }
            set
            {
                mDefaultBufferSize = value;
            }
        }

        /// <summary>
        /// DEFAULT CONSTRUCTOR
        /// </summary>
        /// <param name="RemoteServiceHandlers">LIST OF REMOTE CONTROLLERS TO BE USED FOR FILE STORAGE AND RETRIEVAL</param>
        public RemoteStorageService(string[] RemoteServiceHandlers)
        {
            //ADD THE LIST OF CONTROLLERS PASSED IN TO THE DEFAULT LIST OF CONTROLLERS WE CAN ACCESS
            mRemoteServiceHandlers = RemoteServiceHandlers;

            //MAKE SURE WE DO NOT TIMEOUT DURING AN UPLOAD (DEPENDING ON FILE SIZE THIS MAY TAKE SOME TIME)
            tmpService.Timeout = System.Threading.Timeout.Infinite;
        }

        /// <summary>
        /// UPLOADS A FILE TO A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="stream">STREAM TO PULL DATA FROM</param>
        /// <param name="folder">DESTINATION FOLDER TO STORE REMOTE FILE</param>
        /// <param name="fileName">FILENAME OF THE REMOTE FILE</param>
        /// <param name="overWrite">DETERMINES IF THE FILE SHOULD BE OVERWRITTEN SHOULD IT EXIST</param>
        /// <param name="replicationCount">NUMBER OF TIMES TO REPLICATE THE FILE</param>
        /// <param name="extraInfo">ANY ADDITION INFORMATION TO BE STORED ABOUT THIS FILE</param>
        /// <returns>RETURNS TRUE WHEN THE FILE IS UPLOADED AND FALSE IF IT FAILS</returns>
        public bool UploadFile(Stream stream, string folder, string fileName, bool overWrite, int replicationCount, ExtraInfo[] extraInfo)
        {
            byte[] buffer = new byte[mDefaultBufferSize];
            int count = 0;

            //LOOP THROUGH ALL OF THE CONTROLLER UNITS ATTEMPTING TO UPLOAD THIS FILE UNTIL WE SUCCEED
            foreach (string tmpHandler in RemoteServiceHandlers.Shuffle())
            {
                //SET THE CONTROLLER = TO THE CURRENT ONE FROM OUR LIST
                tmpService.Url = tmpHandler;

                try
                {
                    //THIS WILL CAUSE A CONTROLLER TO CREATE A TEMP FILE TO BEGIN HOLDING UPLOADED CHUNKS
                    string tmpFileID = tmpService.BeginPutFileChunk(folder, fileName, replicationCount, overWrite, extraInfo);

                    //IF THE STREAM CAN SEEK THEN RETURN BACK TO 0
                    if (stream.CanSeek)
                        stream.Seek(0, SeekOrigin.Begin);

                    //WHILE WE ARE STILL READING DATA FROM THE STREAM KEEP UPLOADING IT
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        tmpService.PutFileChunk(tmpFileID, buffer.Take(count).ToArray());
                    }

                    //THIS WILL FORCE THE CONTROLLER TO DISTRIBUTE THE FILE TO THE REMOTE STORAGE LOCATIONS
                    return tmpService.EndPutFileChunk(tmpFileID);
                }
                catch
                {
                    //CATCH EXCEPTIONS AND MOVE ON TO TRY A DIFFERENT REMOTE STORAGE
                }
            }

            //IF WE HAVE MADE IT THIS FAR THEN THE FILE DID NOT UPLOAD CORRECTLY SO WE FAILED
            return false;
        }

        /// <summary>
        /// UPLOADS A FILE TO A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="file">THIS IS THE LOCAL FILE TO BE UPLOADED</param>
        /// <param name="folder">DESTINATION FOLDER TO STORE REMOTE FILE</param>
        /// <param name="fileName">FILENAME OF THE REMOTE FILE</param>
        /// <param name="overWrite">DETERMINES IF THE FILE SHOULD BE OVERWRITTEN SHOULD IT EXIST</param>
        /// <param name="replicationCount">NUMBER OF TIMES TO REPLICATE THE FILE</param>
        /// <param name="extraInfo">ANY ADDITION INFORMATION TO BE STORED ABOUT THIS FILE</param>
        /// <returns>RETURNS TRUE WHEN THE FILE IS UPLOADED AND FALSE IF IT FAILS</returns>
        public bool UploadFile(string file, string folder, string fileName, bool overWrite, int replicationCount, ExtraInfo[] extraInfo)
        {
            //CREATE A FILE STREAM THEN CALL THE DEFAULT UPLOAD FILE USING THAT STREAM
            using (Stream tmpStream = File.OpenRead(file))
            {
                //RETURN THE RESULT OF THE DEFAULT CALL
                return UploadFile(tmpStream, folder, fileName, overWrite, replicationCount, extraInfo);
            }
        }

        /// <summary>
        /// UPLOADS A FILE TO A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="fileData">BYTE ARRAY CONTAINING THE FILE DATA TO BE UPLOADED</param>
        /// <param name="folder">DESTINATION FOLDER TO STORE REMOTE FILE</param>
        /// <param name="fileName">FILENAME OF THE REMOTE FILE</param>
        /// <param name="overWrite">DETERMINES IF THE FILE SHOULD BE OVERWRITTEN SHOULD IT EXIST</param>
        /// <param name="replicationCount">NUMBER OF TIMES TO REPLICATE THE FILE</param>
        /// <param name="extraInfo">ANY ADDITION INFORMATION TO BE STORED ABOUT THIS FILE</param>
        /// <returns>RETURNS TRUE WHEN THE FILE IS UPLOADED AND FALSE IF IT FAILS</returns>
        public bool UploadFile(byte[] fileData, string folder, string fileName, bool overWrite, int replicationCount, ExtraInfo[] extraInfo)
        {
            //CREATE A MEMORY STREAM HOLDING THE DATA THAT IS TO BE UPLOADED THEN CALL THE DEFAULT UPLOAD
            using (MemoryStream tmpStream = new MemoryStream(fileData))
            {
                //RETURN THE RESULT OF THE DEFAULT FILE UPLOAD
                return UploadFile(tmpStream, folder, fileName, overWrite, replicationCount, extraInfo);
            }
        }

        /// <summary>
        /// DOWNLOADS A FILE FROM A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE REMOTE FILE IS STORED</param>
        /// <param name="fileName">NAME OF THE REMOTE FILE TO RETRIEVE</param>
        /// <param name="buffer">BUFFER WHERE FILE DATA IS TO BE STORED</param>
        /// <returns>RETURN TRUE IF THE FILE DOWNLOAD SUCCEEDED OR FALSE IF IT FAILS</returns>
        public bool DownloadFile(string folder, string fileName, Stream stream)
        {
            //LOOP THROUGH ALL POSSIBLE CONTROLLERS IN ORDER TO DOWNLOAD THE FILE
            foreach (string tmpHandler in RemoteServiceHandlers.Shuffle())
            {
                tmpService.Url = tmpHandler;

                try
                {
                    //INITIATE THE DOWNLOAD OF THE FILE TO THE SELECTED CONTROLLER
                    string fileID = tmpService.BeginGetFileChunk(folder, fileName);

                    if (fileName != null)
                    {
                        byte[] tmpBuffer;

                        //WHILE DATA IS COMING BACK KEEP READING THE FILE
                        while ((tmpBuffer = tmpService.GetFileChunk(fileID, mDefaultBufferSize)) != null)
                        {
                            //APPEND THE DATA RECEIVED ONTO THE STREAM PASSED IN
                            stream.Write(tmpBuffer, 0, tmpBuffer.Length);
                        }

                        //CALL THE END FUNCTION SO THE SERVER WILL CLEAN ITSELF UP
                        tmpService.EndGetFileChunk(fileID);

                        //RETURN TRUE SO THE USER KNOWS THE FILE DOWNLOADED
                        return true;
                    }
                }
                catch
                {
                    //CATCH EXCEPTIONS AND MOVE ON TO TRY A DIFFERENT REMOTE STORAGE
                }
            }

            //IF WE MADE IT THIS FAR THEN THE FILE DID NOT DOWNLOAD
            return false;
        }

        /// <summary>
        /// DOWNLOADS A FILE FROM A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE REMOTE FILE IS STORED</param>
        /// <param name="fileName">NAME OF THE REMOTE FILE TO RETRIEVE</param>
        /// <param name="outFile">NAME OF THE LOCAL FILE WHERE TO DOWNLOAD THE FILE TO</param>
        /// <returns>RETURN TRUE IF THE FILE DOWNLOAD SUCCEEDED OR FALSE IF IT FAILS</returns>
        public bool DownloadFile(string folder, string fileName, string outFile)
        {
            //CREATE A NEW FILE STREAM ON DISK AND THEN BEGIN DOWNLOADING THE FILE INTO IT
            using (Stream tmpStream = File.Create(outFile))
            {
                //RETURN THE RESULT OF THE DEFAULT FILE DOWNLOAD
                return DownloadFile(folder, fileName, tmpStream);
            }
        }

        /// <summary>
        /// DOWNLOADS A FILE FROM A REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE REMOTE FILE IS STORED</param>
        /// <param name="fileName">NAME OF THE REMOTE FILE TO RETRIEVE</param>
        /// <returns>RETURNS A BYTE ARRAY CONTAINGING THE DOWNLOADED DATA</returns>
        public byte[] DownloadFile(string folder, string fileName)
        {
            //CREATE A NEW MEMORY STREAM AND BEGIN DOWNLOADING THE FILE INTO IT
            using (MemoryStream tmpMemStream = new MemoryStream())
            {
                //CALL THE DOWNLOAD FUNCTION AND WAIT FOR IT TO FINISH
                DownloadFile(folder, fileName, tmpMemStream);

                //RETURN THE RESULT OF THE MEMORY STREAM IN FORM OF A BYTE ARRAY
                return tmpMemStream.ToArray();
            }
        }

        /// <summary>
        /// DELETES A FILE FROM THE REMOTE STORAGE SERVERS
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE FILE IS LOCATED</param>
        /// <param name="fileName">FILENAME OF THE FILE TO DELETE</param>
        /// <returns>TRUE FOR A SUCCESSFUL DOWNLOAD OTHERWISE WILL THROW AN EXCEPTION</returns>
        public bool DeleteFile(string folder, string fileName)
        {
            //LOOP THROUGH THE HANDLERS AND TRY TO DELETE IT FROM ONE WHICH WILL PROPOGATE TO THE REST
            foreach (string tmpHandler in RemoteServiceHandlers.Shuffle())
            {
                //SET THE URL
                tmpService.Url = tmpHandler;
                
                //CALL THE DELETE FILE FUNCTION ON THE REMOTE SERVER
                return tmpService.DeleteFile(folder, fileName);
            }

            //SOMETHING FAILED SO RETURN FALSE
            return false;
        }
    }
}
