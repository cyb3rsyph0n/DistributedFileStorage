using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Configuration;
using System.Diagnostics;

namespace DistributedFileStorageService.Handlers
{
    /// <summary>
    /// STORES SERVER SETTINGS IN WEB.CONFIG FILE
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// DEFINES IF THIS INSTANCE IS CONFIGURED AS A SERVER OR NOT
        /// </summary>
        private bool mIsController = false;
        public bool IsController
        {
            get
            {
                return mIsController;
            }
        }

        /// <summary>
        /// OTHER SERVERS WHICH ARE RUNNING THIS WEBSERVICE / APPLICATION FOR FILE STORAGE
        /// </summary>
        private List<RemoteStorageLocation> mRemoteStorage = new List<RemoteStorageLocation>();
        public List<RemoteStorageLocation> RemoteStorage
        {
            get
            {
                return mRemoteStorage;
            }
        }

        /// <summary>
        /// HOLDS THE TEMPORARY FILE PATH WHERE FILES ARE HELD WHILE UPLOADED TO A STORAGE SERVER
        /// </summary>
        private string mTempFolder = null;
        public string TempFolder
        {
            get
            {
                return mTempFolder;
            }
        }

        /// <summary>
        /// LOADS A WEB.CONFIG SECTION INTO A SERVERSETTINGS CLASS
        /// </summary>
        /// <param name="section">XML NODE WHICH CONTAINS CONFIG TO LOAD</param>
        /// <returns>RETURNS A LOADED SERVERSETTINGS CLASS</returns>
        public static ServerSettings LoadFromXml(XmlNode section)
        {
            ServerSettings tmpReturn = new ServerSettings();

            //IF THERE IS AN ATTRIBUTE FOR IS SERVER DEFINED THEN COPY IT TO THE SERVER SETTINGS
            if (section.Attributes["isController"] != null)
                tmpReturn.mIsController = bool.Parse(section.Attributes["isController"].Value);
            else
                tmpReturn.mIsController = false;

            //IF THERE IS AN ATTRIBUTE FOR A TEMP FOLDER THEN STORE IT IN THE SERVER SETTINGS CLASS OTHERWISE GENERATE A STD ONE
            if (section.Attributes["TempFolder"] != null)
                if (section.Attributes["TempFolder"].Value.StartsWith("~"))
                    tmpReturn.mTempFolder = HttpContext.Current.Server.MapPath(section.Attributes["TempFolder"].Value);
                else
                    tmpReturn.mTempFolder = section.Attributes["TempFolder"].Value;
            else
                tmpReturn.mTempFolder = HttpContext.Current.Server.MapPath("~/Temp/");

            //POPULATE THE REMOTE STORAGE LIST
            foreach (XmlNode tmpNode in section.SelectNodes("Storage/RemoteServer"))
            {
                tmpReturn.RemoteStorage.Add(new RemoteStorageLocation(tmpNode.Attributes["Path"].Value, tmpNode.Attributes["AccessMode"].Value));
            }

            //VALIDATE THE CONTENTS OF THE RETURN
            if (!ValidateConfig(tmpReturn))
                return null;
            else
                return tmpReturn;
        }

        private static bool ValidateConfig(ServerSettings tmpReturn)
        {
#if !DEBUG
            //VERIFY THERE ARE NOT DUPLICATE REMOTE STORAGE LOCATIONS
            if ((from a in tmpReturn.RemoteStorage group a by a.Path.ToLower()).Where(b => b.Count() > 1).Count() != 0)
                return false;
#endif
            //IF WE HAVE MADE IT THIS FAR THEN THE CONFIG FILE MUST BE OK
            return true;
        }

        public struct RemoteStorageLocation
        {
            public string Path;
            public string AccessMode;

            public RemoteStorageLocation(string path, string accessMode)
            {
                Path = path;
                AccessMode = accessMode.ToLower();
            }
        }
    }

    /// <summary>
    /// HANDLER FOR PULLING INFORMATION FROM THE WEB.CONFIG FILE FOR SERVER SETTINGS
    /// </summary>
    public class ServerSettingsHandler : IConfigurationSectionHandler
    {
        #region IConfigurationSectionHandler Members

        public object Create(object parent, object configContext, XmlNode section)
        {
            return ServerSettings.LoadFromXml(section);
        }

        #endregion
    }

}
