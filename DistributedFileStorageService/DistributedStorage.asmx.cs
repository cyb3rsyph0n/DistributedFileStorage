using System;
using System.IO;
using System.Net;
using System.Web;
using System.Linq;
using System.Collections;
using System.Configuration;
using System.Web.Services;
using System.Collections.Generic;
using System.Security.Cryptography;
using DistributedFileStorageCommon;
using System.Text.RegularExpressions;
using DistributedFileStorageService.Handlers;
using DistributedFileStorageService.DBAccess;
using System.Diagnostics;
using System.Data.Linq.SqlClient;
using System.Collections.Specialized;

namespace DistributedFileStorageService
{
    /// <summary>
    /// Summary description for Service1
    /// </summary>
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class DistributedStorage : System.Web.Services.WebService
    {
        /// <summary>
        /// HOLDS LOADED SERVER CONFIG
        /// </summary>
        private ServerSettings serverSettings = ((ServerSettings)ConfigurationManager.GetSection("ServerSettings"));

        /// <summary>
        /// DEFAULT CONSTRUCTOR
        /// </summary>
        public DistributedStorage()
        {
            //VERIFY WE HAVE SUCCESSFULLY LOADED A VALID CONFIG FILE
            if (serverSettings == null)
            {
                throw new Exception("Error detected in web.config file!");
            }

            //IF THIS IS NOT A SERVER THEN CRASH RIGHT NOW AND DO NOT ALLOW ANYTHING TO BE DONE
            if (!serverSettings.IsController)
            {
                throw new Exception("This system is not configured to be a server only a repository!");
            }
        }

        /// <summary>
        /// RETRIEVES REMOTE STORAGE LOCATIONS FROM CONFIG FILE
        /// </summary>
        /// <returns></returns>
        [WebMethod(Description = "RETRIEVES REMOTE STORAGE LOCATIONS FROM CONFIG FILE")]
        public string[] GetStorageLocations()
        {
            return serverSettings.RemoteStorage.Select(a => a.Path).ToArray();
        }

        /// <summary>
        /// DISTRIBUTES THE FILES AMONGST THE REMOTE STORAGE LOCATIONS
        /// </summary>
        /// <param name="folder">FOLDER WHERE FILE IS TO BE STORED</param>
        /// <param name="fileName">FILENAME OF THE FILE BEING STORED</param>
        /// <param name="replicateCount">NUMBERS OF TIMES TO REPLICATE THE FILE NOT INCLUDING THE INITIAL</param>
        /// <param name="tmpFileName">LOCATION OF THE LOCAL FILE TO UPLOAD</param>
        /// <param name="overWrite">DETERMINE IF THE FILE SHOULD BE OVERWRITTEN IF IT EXISTS</param>
        /// <param name="extraInfo">EXTRA INFO TO STORE ABOUT THE FILE, WARNING: THESE PARAMETERS WILL BE STORED IN PLAIN TEXT</param>
        /// <returns>RETURNS TRUE IF THE FILE WAS SUCCESSFULLY STORED OR FALSE IF IT FAILED</returns>
        private bool DistributeFile(string folder, string fileName, int replicateCount, string tmpFileName, bool overWrite, ExtraInfo[] extraInfo)
        {
            List<string> successfulServers = new List<string>();

            //ALL BELOW CHECKS ARE REALLY DONE TO MAKE SURE THE FILE DOES NOT EXISTS IN THE DATABASE ALREADY
            //VERIFY THE FILENAME AND FOLDER ARE VALID
            if (Regex.Match(fileName, @"[\/:*?""<>|]").Success || Regex.Match(folder, @"[:*?""<>|]").Success)
                throw new ArgumentException("Invalid character(s) detected in filename and or folder");

            //CHECK THE FORMAT OF THE FOLDER
            folder = FormatFolder(folder);

            //IF WE ARE NOT SUPPOSED TO OVERWRITE FILES THEN MAKE SURE IT DOES NOT ALREADY EXISTS IN THE FIRST DB IN THE LIST (IN THEORY IF IT DOES NOT HAVE IT NONE OF THEM SHOULD HAVE IT)
            if (!overWrite)
            {
                using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray().Shuffle()[0].ConnectionString))
                {
                    //IF WE FIND A REFERENCE TO THIS FILE THEN WE NEED TO THROW AN ERROR
                    if ((from a in tmpDB.FileMarkers where a.FileName == fileName && a.Folder == folder select a).Count() != 0)
                        throw new Exception("File already exists");
                }
            }
            else
            {
                //DELETE THE FILE MARKER FROM ALL THE DATABASE SERVERS IN THE CONNECTION STRING LIST
                bool firstLoop = true;
                foreach (ConnectionStringSettings tmpConnection in ConfigurationManager.ConnectionStrings)
                {
                    //CREATE A NEW INSTANCE OF THE DATABASE ACCESS
                    using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(tmpConnection.ConnectionString))
                    {
                        //DELETE ALL OF THE FILELOCATIONS FROM THE DATABASE
                        IEnumerable<FileMarker> tmpMarkers = tmpDB.FileMarkers.Where(a => a.Folder == folder && a.FileName == fileName);

                        //ATTEMPT TO DELETE THE REMOTE FILE TO CLEAR DISK SPACE
                        WebClient tmpClient = new WebClient();
                        if (tmpMarkers.Count() != 0 && firstLoop)
                            foreach (FileLocation tmpLocation in tmpMarkers.First().FileLocations)
                                try
                                {
                                    tmpClient.DownloadData(string.Format("{0}DeleteFile.aspx?folder={1}&filename={2}", tmpLocation.Location, Server.UrlEncode(tmpLocation.FileMarker.Folder), Server.UrlEncode(tmpLocation.FileMarker.FileName)));
                                }
                                catch
                                {
                                    //DO NOTHING IF WE ARE UNABLE TO DELETE THE FILE FOR ANY REASON
                                }

                        foreach (FileMarker tmpMarker in tmpMarkers)
                            tmpDB.FileLocations.DeleteAllOnSubmit(tmpMarker.FileLocations);

                        //DELETE ALL OF THE FILEMARKERS FROM THE DATABASE
                        tmpDB.FileMarkers.DeleteAllOnSubmit(tmpMarkers);

                        //SUBMIT THE CHANGES TO THE DATABASE
                        tmpDB.SubmitChanges();

                        //REMEMBER THIS IS NOT THE FIRST LOOP ANYMORE SO WE DO NOT TRY TO DELETE FILES AGAIN IF IN PMR STATE
                        firstLoop = false;
                    }
                }
            }

            //NOW THE FUN PART TRY TO STORE THE FILE ON ANY SERVER WHICH WILL TAKE IT
            try
            {
                //IF THE TEMP FOLDER DOES NOT EXISTS THEN CREATE IT SO WE CAN STORE THE FILE ON DISK AND UPLOAD IT TO THE REMOTE STORAGE LOCATION
                if (!Directory.Exists(serverSettings.TempFolder))
                    Directory.CreateDirectory(serverSettings.TempFolder);

                //USE WEBCLIENT FOR SIMPLE UPLOADS (THIS MAY NEED TO BE LOOKED INTO FOR DIRECT STREAM WRITING ON LARGE FILES)
                WebClient tmpClient = new WebClient();
                foreach (string chosenServer in (from a in serverSettings.RemoteStorage where a.AccessMode.Contains("w") select a.Path).ToArray().Shuffle())
                {
                    try
                    {
                        string result = UploadFile(string.Format("{0}PutFile.aspx?folder={1}&filename={2}", chosenServer, Server.UrlEncode(folder), Server.UrlEncode(fileName)), tmpFileName);

                        //IF THE FILE UPLOAD WAS A SUCCESS THEN TRACK THE SERVER WE PLACED IT ON
                        if (result.ToLower() == "success")
                        {
                            successfulServers.Add(chosenServer);

                            //IF WE HAVE UPLOADED TO ENOUGH SERVERS TO SATISFY THE REPLICATION NEEDS THEN EXIT OUT AND CONTINUE ON
                            if (successfulServers.Count == replicateCount + 1)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace(true));
                    }
                }
            }
            catch (Exception e)
            {
                //HANDLE ERROR IF TEMP FOLDER DIES
                Logging.WriteEntry(e.Message, e, new StackTrace(true));
            }

            //IF THE FILE WAS SAVED SUCCESSFULLY THEN RETURN TRUE TO THE USER AND SAVE THE MARKER FILE
            if (successfulServers.Count == replicateCount + 1)
            {
                //CREATE A TEMPORARY MARKER TO BE STORED IN THE DATABASE
                FileInfo tmpFileInfo = new FileInfo(tmpFileName);
                FileMarker tmpMarker = new FileMarker();
                tmpMarker.FileMarkerID = Guid.NewGuid().ToString();
                tmpMarker.FileName = fileName;
                tmpMarker.Folder = folder;
                tmpMarker.Length = tmpFileInfo.Length;
                tmpMarker.Hash = File.Open(tmpFileName, FileMode.Open).ComputeMD5(true);
                tmpMarker.LastWriteTime = DateTime.Now;
                tmpMarker.LastModTime = DateTime.Now;
                tmpMarker.LastReadTime = DateTime.Now;
                tmpMarker.ExtraInfo = extraInfo != null ? System.Text.ASCIIEncoding.ASCII.GetBytes(extraInfo.Serialize().Compress()) : new byte[] { };
                tmpMarker.FileLocations.AddRange(successfulServers.Select(a => new FileLocation() { Location = a, LocationID = Guid.NewGuid().ToString() }).ToArray());

                //WRITE THE FILE MARKER TO ALL THE DATABASE SERVERS IN THE CONNECTION STRING LIST
                foreach (ConnectionStringSettings tmpConnection in ConfigurationManager.ConnectionStrings)
                {
                    //CREATE A NEW INSTANCE OF THE DATABASE ACCESS
                    using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(tmpConnection.ConnectionString))
                    {
                        //ADD THE FILE MARKER AND SUBMIT THE CHANGES
                        tmpDB.FileMarkers.InsertOnSubmit(tmpMarker);
                        tmpDB.SubmitChanges();
                    }
                }

                //RETURN THE SUCCESS TO THE CALLING FUNCTION
                return true;
            }
            else
            {
                //ATTEMPT TO NOW GO BACK AND DELETE ALL THE FILES WHICH DID SUCCESSFULLY UPLOAD
                WebClient tmpClient = new WebClient();
                foreach (string chosenServer in successfulServers)
                {
                    try
                    {
                        string result = System.Text.ASCIIEncoding.ASCII.GetString(tmpClient.DownloadData(string.Format("{0}DeleteFile.aspx?folder={1}&filename={2}", chosenServer, Server.UrlEncode(folder), Server.UrlEncode(fileName))));
                    }
                    catch (Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace(true));
                    }
                }

                //LET THE CALLING FUNCTION KNOW THE STORE FILE FAILED
                return false;
            }
        }

        /// <summary>
        /// USED TO STORE A FILE SOMEWHERE IN THE LIST OF REMOTE SERVERS
        /// </summary>
        /// <param name="folder">FOLDER PATH TO STORE THE FILE</param>
        /// <param name="fileName">FILENAME FOR STORAGE</param>
        /// <param name="replicateCount">NUMBER OF TIMES TO STORE THE FILE NOT INCLUDING THE INITIAL</param>
        /// <param name="fileData">BYTE ARRAY CONTAINING ALL THE FILE DATA TO BE SAVED</param>
        /// <param name="overWrite">DETERMINES IF THE FILE SHOULD BE OVERWRITTEN IF IT EXISTS</param>
        /// <param name="extraInfo">EXTRA INFO TO STORE ABOUT THE FILE, WARNING: THESE PARAMETERS WILL BE STORED IN PLAIN TEXT</param>
        /// <returns>RETURNS TRUE IF THE FILE SAVE WAS SUCCESSFULL OR FALSE IF IT FAILED.</returns>
        [WebMethod(Description = "USED TO STORE A FILE SOMEWHERE IN THE LIST OF REMOTE SERVERS")]
        public bool PutFile(string folder, string fileName, int replicateCount, byte[] fileData, bool overWrite, ExtraInfo[] extraInfo)
        {
            string tmpFileName = Path.Combine(serverSettings.TempFolder, Guid.NewGuid().ToString());

            try
            {
                File.WriteAllBytes(tmpFileName, fileData);
                return DistributeFile(folder, fileName, replicateCount, tmpFileName, overWrite, extraInfo);
            }
            catch (Exception e)
            {
                Logging.WriteEntry(e.Message, e, new StackTrace());
                throw e;
            }
            finally
            {
                //ATTEMPT TO DELETE THE TEMP FILE
                try
                {
                    File.Delete(tmpFileName);
                }
                catch (Exception e)
                {
                    Logging.WriteEntry(e.Message, e, new StackTrace());
                }
            }
        }

        /// <summary>
        /// USED TO CREATE A TEMP FILE WHICH WE WILL BEGIN SENDING CHUNKS OF DATA TO IN EFFORT TO STORE THE FILES ACCROSS THE DFS
        /// </summary>
        /// <param name="folder">FOLDER WHERE FINAL FILE WILL BE STORED</param>
        /// <param name="fileName">FILENAME OF FILE TO STORE</param>
        /// <param name="replicateCount">NUMBER OF TIMES TO REPLICATE THE FILE NOT INCLUDING THE INITIAL</param>
        /// <param name="overWrite">DETERMINES IF THE FILE SHOULD BE OVERWRITTEN IF IT EXISTS</param>
        /// <param name="extraInfo">EXTRA INFO TO STORE ABOUT THE FILE, WARNING: THESE PARAMETERS WILL BE STORED IN PLAIN TEXT</param>
        /// <returns>RETURNS A TEMPORARY FILEID WHICH IS USED DURING THE CHUNK SENDS AND END PUTFILE</returns>
        [WebMethod(Description = "USED TO CREATE A TEMP FILE WHICH WE WILL BEGIN SENDING CHUNKS OF DATA TO IN EFFORT TO STORE THE FILES ACCROSS THE DFS")]
        public string BeginPutFileChunk(string folder, string fileName, int replicateCount, bool overWrite, ExtraInfo[] extraInfo)
        {
            string fileID = Guid.NewGuid().ToString();
            string tmpFileName = Path.Combine(serverSettings.TempFolder, fileID);
            string tmpMarkerName = tmpFileName + "._marker_";
            TempFileMarker tmpMarker = new TempFileMarker() { Folder = folder, FileName = fileName, ReplicateCount = replicateCount, OverWrite = overWrite, ExtraInfo = extraInfo };

            try
            {
                //CREATE THE TEMPORARY FILE ID WHICH WILL HOLD THE DATA UNTIL THE ENDPUTFILE IS CALLED
                File.Create(tmpFileName).Close();

                //WRITE THE MARKER FILE TO DISK SO WE CAN PULL ITS INFORMATION LATER
                File.WriteAllBytes(tmpMarkerName, System.Text.ASCIIEncoding.ASCII.GetBytes(tmpMarker.Serialize()));

                //RETURN THE FILEID SO WE CONTINUE TO APPEND TO THE CORRECT FILE
                return fileID;
            }
            catch (Exception e)
            {
                //IF ANYTHING HAPPENS ATTEMPT TO DELETE THE FILE AND ITS MARKER SINCE IT WILL NOT BE NEEDED
                try
                {
                    File.Delete(tmpFileName);
                    File.Delete(tmpMarkerName);
                }
                catch
                {
                }

                //LOG THE ERROR TO THE DATABASE FOR LATER
                Logging.WriteEntry(e.Message, e, new StackTrace());
            }

            //IF WE HAVE MADE IT THIS FAR THEN THERE WAS AN ERROR SO RETURN NULL
            return null;
        }

        /// <summary>
        /// APPENDS UPLOADED BYTES TO THE FILEID WHICH WAS OBTAINED FROM BEGINPUTFILECHUNK
        /// </summary>
        /// <param name="fileID">TEMPORARY FILEID WHICH WAS HANDED BACK FROM BEGINPUTFILECHUNK</param>
        /// <param name="fileData">ACTUAL ARRAY OF BYTES WHICH ARE TO BE STORED</param>
        /// <returns>RETURNS TRUE IF THE BYTES WERE SUCCESSFULLY APPENDED TO THE TEMP FILE</returns>
        [WebMethod(Description = "APPENDS UPLOADED BYTES TO THE FILEID WHICH WAS OBTAINED FROM BEGINPUTFILECHUNK")]
        public bool PutFileChunk(string fileID, byte[] fileData)
        {
            string tmpFileName = Path.Combine(serverSettings.TempFolder, fileID);

            //IF THE TEMP FILE EXISTS THEN APPEND THE NEW DATA TO THE END OF THE FILE
            if (!File.Exists(tmpFileName))
                throw new FileNotFoundException("Invalid FileID Specified");
            else
            {
                try
                {
                    //OPEN THE FILE FOR WRITING
                    using (FileStream tmpStream = File.OpenWrite(tmpFileName))
                    {
                        //SEEK THE END OF THE FILE AND WRITE THE NEW BYTES
                        tmpStream.Seek(0, SeekOrigin.End);
                        tmpStream.Write(fileData, 0, fileData.Length);
                        tmpStream.Flush();

                        //RETURN THE SUCCESS BACK TO THE CALLING FUNCTION
                        return true;
                    }
                }
                catch (Exception e)
                {
                    //LOG ANY ERROR AND RETURN FALSE TO THE USER
                    Logging.WriteEntry(e.Message, e, new StackTrace());
                    return false;
                }
            }
        }

        /// <summary>
        /// USED TO END THE UPLOAD OF A FILE AND STORE IT ACCROSS THE DFS SYSTEMS
        /// </summary>
        /// <param name="fileID">FILE ID WHICH HAS RECEIVED THE TEMPORARY FILE CHUNKS</param>
        /// <returns>RETURNS THE RESULT OF PUTFILE TRUE FOR SUCCESS AND FALSE FOR FAIL</returns>
        [WebMethod(Description = "USED TO END THE UPLOAD OF A FILE AND STORE IT ACCROSS THE DFS SYSTEMS")]
        public bool EndPutFileChunk(string fileID)
        {
            string tmpFileName = Path.Combine(serverSettings.TempFolder, fileID);
            string tmpMarkerName = tmpFileName + "._marker_";

            //IF THE TEMP FILE EXISTS THEN BEGIN THE REAL PUTFILE WHICH STORES IT ACROSS ALL THE SERVERS AND WRITES THE INFORMATION TO THE DATABASE
            if (!File.Exists(tmpFileName) || !File.Exists(tmpMarkerName))
                throw new FileNotFoundException("Invalid FileID Specified");
            else
            {
                try
                {
                    TempFileMarker tmpDiskMarker = File.ReadAllText(tmpMarkerName).Deserialize<TempFileMarker>();

                    //CALL THE ORIGINAL PUT FILE AND RETURN ITS RESULT
                    return DistributeFile(tmpDiskMarker.Folder, tmpDiskMarker.FileName, tmpDiskMarker.ReplicateCount, tmpFileName, tmpDiskMarker.OverWrite, tmpDiskMarker.ExtraInfo);
                }
                catch (Exception e)
                {
                    //LOG ANY EXCEPTION WHICH OCCURED DURING THE ATTEMPTED MOVE
                    Logging.WriteEntry(e.Message, e, new StackTrace());
                }
                finally
                {
                    //ATTEMPT TO DELETE THE TEMP FILE
                    try
                    {
                        File.Delete(tmpFileName);
                    }
                    catch (Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace());
                    }

                    //ATTEMPT TO DELETE THE MARKER FILE
                    try
                    {
                        File.Delete(tmpMarkerName);
                    }
                    catch (Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace());
                    }
                }
            }

            //IF WE HAVE MADE IT THIS FAR THEN THERE WAS AN ERROR SO RETURN FALSE;
            return false;
        }

        /// <summary>
        /// USED TO ABORT A FILE CHUNK UPLOAD
        /// </summary>
        /// <param name="fileID">TEMP FILEID WHICH SHOULD BE CANCELLED</param>
        [WebMethod(Description = "USED TO ABORT A FILE CHUNK UPLOAD")]
        public void AbortPutFileChunk(string fileID)
        {
            string tmpFileName = Path.Combine(serverSettings.TempFolder, fileID);
            string tmpMarkerFileName = tmpFileName + "._marker_";

            //ATTEMPT TO DELETE THE TMP FILE WHICH CONTAINED ANY DATA THAT MAY HAVE BEEN UPLOADED
            try
            {
                File.Delete(tmpFileName);
            }
            catch (Exception e)
            {
                Logging.WriteEntry(e.Message, e, new StackTrace());
            }

            //ATTEMPT TO DELETE THE TMPMARKER FILE WHICH CONTAINED THE INCOMING PARAMETERS
            try
            {
                File.Delete(tmpMarkerFileName);
            }
            catch (Exception e)
            {
                Logging.WriteEntry(e.Message, e, new StackTrace());
            }
        }

        /// <summary>
        /// USED TO RETRIEVE A FILE FROM SOMEWHERE IN THE LIST OF REMOTE SERVERS
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE FILE IS LOCATED</param>
        /// <param name="fileName">FILENAME OF THE FILE TO RETRIEVE</param>
        /// <returns>BYTE ARRAY CONTAINING ALL OF THE DATA IN THE FILE THAT WAS RETRIEVED</returns>
        [WebMethod(Description = "USED TO RETRIEVE A FILE FROM SOMEWHERE IN THE LIST OF REMOTE SERVERS")]
        public byte[] GetFile(string folder, string fileName)
        {
            //USE A BASIC WEBCLIENT TO ATTEMPT TO DOWNLOAD THE FILE FROM THE STORAGE LOCATION
            WebClient tmpClient = new WebClient();

            //MAKE SURE THE FOLDER IS IN THE PROPER FORMAT FOR THE DATABASE
            folder = FormatFolder(folder);

            //USED THE FIRST CONNECTION IN THE DATABASE WHILE PREFORMING READS IT IS CONSIDERED THE "MASTER"
            using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray().Shuffle()[0].ConnectionString))
            {
                FileMarker tmpMarker = tmpDB.FileMarkers.Where(a => a.Folder == folder && a.FileName == fileName).FirstOrDefault();

                //IF THE DATABASE DID NOT HAVE THIS FILE THEN RETURN AN ERROR BECAUSE FILE WAS NOT FOUND
                if (tmpMarker != null)
                {
                    //FOR ATTEMPT TO DOWNLOAD THE FILE FROM EACH LOCATION "SHUFFLED" TO HELP LOAD BALANCE THE SERVERS
                    foreach (FileLocation tmpLocation in tmpMarker.FileLocations.ToArray().Shuffle())
                    {
                        try
                        {
                            byte[] result = tmpClient.DownloadData(string.Format("{0}GetFile.aspx?folder={1}&filename={2}", tmpLocation.Location, Server.UrlEncode(folder), Server.UrlEncode(fileName)));

                            //IF THIS FILE MATCHES WHAT THE HASH SAYS IT SHOULD THEN RETURN IT. OTHERWISE ASSUME ITS CORRUPT
                            if (BitConverter.ToString(MD5.Create().ComputeHash(result)).Replace("-", "") == tmpMarker.Hash)
                            {
                                return result;
                            }
                            else
                            {
                                Logging.WriteEntry(string.Format("Incorrect Hash for File: {0}{1} on RemoteStorage: {2}", folder, fileName, tmpLocation.Location), null, null);
                            }
                        }
                        catch (Exception e)
                        {
                            Logging.WriteEntry(e.Message, e, new StackTrace(true));
                        }
                    }
                }
                else
                {
                    //FILE WAS NOT FOUND SO THROW AN ERROR INSTEAD OF RETURNING
                    throw new Exception("File not found!");
                }
            }

            //IF WE HAVE MADE IT THIS FAR THEN THE FILE SHOULD HAVE BEEN FOUND BUT WE HAVE A PROBLEM
            throw new Exception("Unable to read file please verify storage integrity!");
        }

        /// <summary>
        /// USED TO BEGIN RECEIVING A FILE FROM REMOTE STORAGE
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE FILE IS LOCATED</param>
        /// <param name="fileName">FILENAME TO BEGIN DOWNLOADING</param>
        /// <returns>RETURNS A TEMPORARY ID WHICH WILL BE USED DURING THE GETFILECHUNK AND THE ENDGETFILECHUNK</returns>
        [WebMethod(Description = "USED TO BEGIN RECEIVING A FILE FROM REMOTE STORAGE")]
        public string BeginGetFileChunk(string folder, string fileName)
        {
            string tmpFileID = Guid.NewGuid().ToString();
            string tmpFileName = Path.Combine(serverSettings.TempFolder, tmpFileID);

            //USE A BASIC WEBCLIENT TO ATTEMPT TO DOWNLOAD THE FILE FROM THE STORAGE LOCATION
            WebClient tmpClient = new WebClient();

            //MAKE SURE THE FOLDER IS IN THE PROPER FORMAT FOR THE DATABASE
            folder = FormatFolder(folder);

            //USED THE FIRST CONNECTION IN THE DATABASE WHILE PREFORMING READS IT IS CONSIDERED THE "MASTER"
            using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray().Shuffle()[0].ConnectionString))
            {
                FileMarker tmpMarker = tmpDB.FileMarkers.Where(a => a.Folder == folder && a.FileName == fileName).FirstOrDefault();

                //IF THE DATABASE DID NOT HAVE THIS FILE THEN RETURN AN ERROR BECAUSE FILE WAS NOT FOUND
                if (tmpMarker != null)
                {
                    //FOR ATTEMPT TO DOWNLOAD THE FILE FROM EACH LOCATION "SHUFFLED" TO HELP LOAD BALANCE THE SERVERS
                    foreach (FileLocation tmpLocation in tmpMarker.FileLocations.ToArray().Shuffle())
                    {
                        try
                        {
                            //DOWNLOAD THE FILE TO A TEMPORARY STORING LOCATION WHERE WE CAN BEGIN TO RECEIVE THE FILE CHUNKS FROM
                            tmpClient.DownloadFile(string.Format("{0}GetFile.aspx?folder={1}&filename={2}", tmpLocation.Location, Server.UrlEncode(folder), Server.UrlEncode(fileName)), tmpFileName);

                            //IF THE FILES HASH IS CORRECT THEN RETURN TO CALLING FUNCTION, ELSE DELETE THE TEMP FILE AND TRY AGAIN
                            if (File.OpenRead(tmpFileName).ComputeMD5(true) == tmpMarker.Hash)
                            {
                                File.WriteAllText(tmpFileName + "._placement_", "0");
                                return tmpFileID;
                            }
                            else
                            {
                                Logging.WriteEntry(string.Format("Incorrect Hash Value on file {0}{1}", folder, fileName), null, null);
                                File.Delete(tmpFileName);
                            }
                        }
                        catch (Exception e)
                        {
                            Logging.WriteEntry(e.Message, e, new StackTrace(true));
                        }
                    }
                }
                else
                {
                    //FILE WAS NOT FOUND SO THROW AN ERROR INSTEAD OF RETURNING
                    throw new Exception("File not found!");
                }
            }

            //IF WE HAVE MADE IT THIS FAR THEN THE FILE SHOULD HAVE BEEN FOUND BUT WE HAVE A PROBLEM
            throw new Exception("Unable to read file please verify storage integrity!");
        }

        /// <summary>
        /// USED TO RECEIVE A CHUNK OF A FILE
        /// </summary>
        /// <param name="fileID">FILEID OF THE FILE TO DOWNLOAD A CHUNK OF</param>
        /// <param name="length">AMOUNT OF BYTES TO BE DOWNLOADED DURING THIS CHUNK</param>
        /// <returns>RETURNS A BYTE ARRAY OF DATA FROM THE FILE</returns>
        [WebMethod(Description = "USED TO RECEIVE A CHUNK OF A FILE")]
        public byte[] GetFileChunk(string fileID, int length)
        {
            string fileName = Path.Combine(serverSettings.TempFolder, fileID);
            string placementFile = fileName + "._placement_";
            long startPoint = long.Parse(File.ReadAllText(placementFile));

            if (File.Exists(fileName))
            {
                using (Stream tmpStream = File.OpenRead(fileName))
                {
                    byte[] buffer = new byte[length];
                    int count;

                    tmpStream.Seek(startPoint, SeekOrigin.Begin);
                    
                    if (tmpStream.Position == tmpStream.Length)
                        return null;

                    count = tmpStream.Read(buffer, 0, buffer.Length);
                    startPoint += count;

                    File.WriteAllText(placementFile, startPoint.ToString());

                    return buffer.Take(count).ToArray();
                }
            }
            else
            {
                throw new FileNotFoundException("Invalid FileID Specified");
            }
        }

        /// <summary>
        /// USED TO END GETTING OF FILE CHUNKS.  PREFORMS ALL CLEANUP
        /// </summary>
        /// <param name="fileID">FILEID OF THE FILE TO STOP DOWNLOADING</param>
        [WebMethod(Description = "USED TO END GETTING OF FILE CHUNKS.  PREFORMS ALL CLEANUP")]
        public void EndGetFileChunk(string fileID)
        {
            string fileName = Path.Combine(serverSettings.TempFolder, fileID);
            string placementFile = fileName + "._placement_";

            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch(Exception e)
                {
                    Logging.WriteEntry(e.Message, e, new StackTrace());
                }
                try
                {
                    File.Delete(placementFile);
                }
                catch (Exception e)
                {
                    Logging.WriteEntry(e.Message, e, new StackTrace());
                }
            }
        }

        /// <summary>
        /// DELETES A FILE FROM THE REMOTE STORAGE LOCATION
        /// </summary>
        /// <param name="folder">FOLDER WHERE THE FILE TO BE DELETED IS LOCATED</param>
        /// <param name="fileName">FILENAME OF THE FILE TO DELETE</param>
        /// <returns>TRUE FOR A SUCCESSFUL DELETE OR THROWS AN EXCEPTION BACK</returns>
        [WebMethod(Description = "USED TO DELETE A FILE FROM DISTRIBUTED STORAGE")]
        public bool DeleteFile(string folder, string fileName)
        {
            //USE A BASIC WEBCLIENT TO ATTEMPT TO DOWNLOAD THE FILE FROM THE STORAGE LOCATION
            WebClient tmpClient = new WebClient();

            //MAKE SURE THE FOLDER IS IN THE PROPER FORMAT FOR THE DATABASE
            folder = FormatFolder(folder);

            //USED THE FIRST CONNECTION IN THE DATABASE WHILE PREFORMING READS IT IS CONSIDERED THE "MASTER"
            foreach (ConnectionStringSettings tmpConnection in ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray())
            {
                using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(tmpConnection.ConnectionString))
                {
                    FileMarker tmpMarker = tmpDB.FileMarkers.Where(a => a.Folder == folder && a.FileName == fileName).FirstOrDefault();

                    //IF THE DATABASE DID NOT HAVE THIS FILE THEN RETURN AN ERROR BECAUSE FILE WAS NOT FOUND
                    if (tmpMarker != null)
                    {
                        //FOR ATTEMPT TO DELETE THE FILE FROM EACH OF ITS REMOTE STORAGE LOCATIONS
                        foreach (FileLocation tmpLocation in tmpMarker.FileLocations.ToArray())
                        {
                            try
                            {
                                //DELETE THE FILE FROM THE REMOTE STORAGE LOCATION
                                tmpClient.DownloadData(string.Format("{0}DeleteFile.aspx?folder={1}&filename={2}", tmpLocation.Location, Server.UrlEncode(folder), Server.UrlEncode(fileName)));

                                //REMOVE THE LOCATION FROM THE DB
                                tmpDB.FileLocations.DeleteOnSubmit(tmpLocation);
                            }
                            catch (Exception e)
                            {
                                Logging.WriteEntry(e.Message, e, new StackTrace(true));
                            }
                        }

                        //REMOVE THE MARKER FROM THE DB
                        tmpDB.FileMarkers.DeleteOnSubmit(tmpMarker);

                        //SUBMIT ALL THE CHANGES TO THE DATABASE FOR UPDATING
                        tmpDB.SubmitChanges();
                    }
                    else
                    {
                        //FILE WAS NOT FOUND SO THROW AN ERROR INSTEAD OF RETURNING
                        throw new Exception("File not found!");
                    }
                }
            }

            //RETURN SUCCESSFULL FOR THE DELETE
            return true;
        }

        /// <summary>
        /// USED TO RETRIEVE A DIRECTORY LISTING FOR A GIVEN FOLDER PASSED IN
        /// </summary>
        /// <param name="folder">ROOT FOLDER TO START LISTING FROM</param>
        /// <param name="listOptions">HOW DEEP TO SEARCH FOR FOLDER LISTING</param>
        /// <returns>RETURNS AN ARRAY OF STRINGS REPRESENTING THE FOLDER STRUCTURE</returns>
        [WebMethod(Description = "USED TO RETRIEVE A DIRECTORY LISTING FOR A GIVEN FOLDER PASSED IN")]
        public string[] GetDirectoryListing(string folder, DirectoryListOptions listOptions)
        {
            //MAKE SURE THE FOLDER IS IN THE PROPER FORMAT FOR THE DATABASE
            folder = FormatFolder(folder);

            using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray().Shuffle()[0].ConnectionString))
            {
                int folderDepth = folder.Split('/').Length;
                if (listOptions == DirectoryListOptions.SelectedFolderOnly)
                    return tmpDB.FileMarkers.Where(a => a.Folder.StartsWith(folder)).Select(a => a.Folder).Distinct().ToArray().Where(a => a.Split('/').Length == folderDepth + 1).ToArray();
                else if (listOptions == DirectoryListOptions.AllSubDirectories)
                    return tmpDB.FileMarkers.Where(a => a.Folder.StartsWith(folder)).Select(a => a.Folder).Distinct().ToArray();
                else
                    throw new ArgumentException("Invalid parameter specified for listingOptions");
            }
        }

        /// <summary>
        /// USED TO RETRIEVE A FILE LISTING FOR A GIVEN FOLDER PASSED IN
        /// </summary>
        /// <param name="folder">ROOT FOLDER TO START LISTING FROM</param>
        /// <param name="listOptions">HOW DEEP TO SEARCH FOR FOLDER LISTING</param>
        /// <returns>RETURNS AN ARRAY OF REMOTEFILEINFO REPRESENTING THE FILES IN A GIVEN FOLDER</returns>
        [WebMethod(Description = "USED TO RETRIEVE A FILE LISTING FOR A GIVEN FOLDER PASSED IN")]
        public RemoteFileInfo[] GetFileListing(string folder, DirectoryListOptions listOptions)
        {
            //MAKE SURE THE FOLDER IS IN THE PROPER FORMAT FOR THE DATABASE
            folder = FormatFolder(folder);

            using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToArray().Shuffle()[0].ConnectionString))
            {
                if (listOptions == DirectoryListOptions.SelectedFolderOnly)
                    return tmpDB.FileMarkers.Where(a => a.Folder == folder).Select(a => new RemoteFileInfo() { Folder = a.Folder, FileName = a.FileName, LastWriteTime = a.LastWriteTime, LastModTime = a.LastModTime, LastReadTime = a.LastReadTime, Length = a.Length, RemoteLocations = a.FileLocations.Select(b => b.Location).ToArray() }).ToArray();
                else if (listOptions == DirectoryListOptions.AllSubDirectories)
                    return tmpDB.FileMarkers.Where(a => a.Folder.StartsWith(folder)).Select(a => new RemoteFileInfo() { Folder = a.Folder, FileName = a.FileName, LastWriteTime = a.LastWriteTime, LastModTime = a.LastModTime, LastReadTime = a.LastReadTime, Length = a.Length, RemoteLocations = a.FileLocations.Select(b => b.Location).ToArray() }).ToArray();
                else
                    throw new ArgumentException("Invalid parameter specified for listingOptions");
            }
        }

        /// <summary>
        /// GENERATE THE PROPER FOLDER FORMAT
        /// </summary>
        /// <param name="folder">FOLDER AS ITS PASSED IN FROM THE USER</param>
        /// <returns>RETURNS A PROPERLY FORMATTED FOLDER NAME</returns>
        private string FormatFolder(string folder)
        {
            //MAKE SURE ALL PATHS ARE UNIFORMED AND THEY START WITH A /
            folder = folder.Replace("\\", "/");
            if (!folder.StartsWith("/"))
                throw new ArgumentException("Paths must start with '/'");

            //IF THE PATH DOES NOT HAVE A SLASH AT THE END THEN PLACE ONE THERE
            if (!folder.EndsWith("/"))
                folder = folder + "/";

            //RETURN THE RESULT TO THE CALLING FUNCTION AND REPLACE ANY DOUBLE / WITH A SINGLE /
            return folder.Replace("//", "/");
        }

        /// <summary>
        /// UPLOAD A FILE WITHOUT TIMEOUT TO USING HTTPWEBREQUEST
        /// </summary>
        /// <param name="url">URL OF THE PAGE TO POST THE UPLOAD TO</param>
        /// <param name="fileName">LOCAL FILE NAME TO BE UPLOADED</param>
        /// <returns>RETURNS ANY RESPONSE FROM THE PAGE THE FILE WAS UPLOADED TO</returns>
        private string UploadFile(string url, string fileName)
        {
            FileInfo tmpFileInfo = new FileInfo(fileName);
            HttpWebRequest tmpRequest = (HttpWebRequest)WebRequest.Create(url);
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundaryBytes = ("\r\n--" + boundary + "\r\n").ToByteArray();
            byte[] header = string.Format("Content-Disposition: form-data; name=\"file0\"; filename=\"{0}\"\r\n Content-Type: application/octet-stream\r\n\r\n", tmpFileInfo.Name).ToByteArray();

            //SETUP THE WEB REQUEST FOR FILE UPLOAD
            tmpRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            tmpRequest.Method = "POST";
            tmpRequest.KeepAlive = true;
            tmpRequest.Timeout = System.Threading.Timeout.Infinite;
            //tmpRequest.AllowWriteStreamBuffering = false;
            //tmpRequest.SendChunked = true;

            //BEGIN WRITING THE FILE DATA TO THE STREAM
            using (Stream request = tmpRequest.GetRequestStream())
            {
                //WRITE THE BOUNDARY AND HEADER TO THE STREAM
                request.Write(boundaryBytes, 0, boundaryBytes.Length);
                request.Write(header, 0, header.Length);

                //WRITE THE ENTIRE FILE TO THE OUTBOUND STREAM
                using (Stream tmpFileStream = File.OpenRead(tmpFileInfo.FullName))
                {
                    byte[] buffer = new byte[1024];
                    int count = 0;
                    while ((count = tmpFileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        request.Write(buffer, 0, count);
                    }
                }

                //WRITE THE BOUNDARY AND FLUSH THE STREAM TO END DATA TRANSFER
                request.Write(boundaryBytes, 0, boundaryBytes.Length);
                request.Flush();
            }

            //BEGIN GETTING THE RETURN FROM THE SERVER
            using (StreamReader tmpReader = new StreamReader(((WebResponse)tmpRequest.GetResponse()).GetResponseStream()))
            {
                //RETURN THE RESULT TO THE CALLING FUNCTION
                return tmpReader.ReadToEnd();
            }
        }
    }

    /// <summary>
    /// HOLDS USER PASSED PARAMETERS ABOUT THE FILE
    /// THESE VALUES CAN BE ANYTHING THE USER DREAMS UP WHICH WILL FIT INTO A STRING
    /// </summary>
    public class ExtraInfo
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    [Serializable]
    public class RemoteFileInfo
    {
        public string Folder { get; set; }
        public string FileName { get; set; }
        public string[] RemoteLocations { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime LastModTime { get; set; }
        public DateTime LastReadTime { get; set; }
        public ExtraInfo[] ExtraInfo { get; set; }
    }

    /// <summary>
    /// TEMPORARY FILE MARKER WHICH HOLDS THE PARAMETERS DURING FILE CHUNK SENDS
    /// </summary>
    [Serializable]
    public class TempFileMarker
    {
        public string Folder { get; set; }
        public string FileName { get; set; }
        public int ReplicateCount { get; set; }
        public bool OverWrite { get; set; }
        public ExtraInfo[] ExtraInfo { get; set; }
    }

    /// <summary>
    /// USED FOR DIRECTORY LISTING
    /// </summary>
    public enum DirectoryListOptions
    {
        AllSubDirectories = 0,
        SelectedFolderOnly = 1
    }
}