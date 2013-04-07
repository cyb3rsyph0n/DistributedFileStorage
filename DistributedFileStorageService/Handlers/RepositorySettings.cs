using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Configuration;

namespace DistributedFileStorageService.Handlers
{
    public class RepositorySettings
    {
        /// <summary>
        /// HOLDS REPOSITORY FOLDER LIST OF WHERE FILES CAN BE STORED THESE WILL BE FILLED IN FIRST COME FIRST SERVE UNTIL ONE IS FULL
        /// </summary>
        private List<RepositoryFolder> mRepositoryFolders = new List<RepositoryFolder>();
        public List<RepositoryFolder> RepositoryFolders
        {
            get
            {
                return mRepositoryFolders;
            }
        }

        /// <summary>
        /// LOADS A WEB.CONFIG SECTION INTO A REPOSITORYSETTINGS CLASS
        /// </summary>
        /// <param name="section">XML NODE WHICH CONTAINS CONFIG TO LOAD</param>
        /// <returns>RETURNS A LOADED REPOSITORY SETTINGS CLASS</returns>
        public static RepositorySettings LoadFromXml(XmlNode section)
        {
            RepositorySettings tmpReturn = new RepositorySettings();
            RepositoryFolder tmpRepoFolder;

            //LOOP THROUGH EACH DATASTORE NODE AND ADD THEM TO THE LIST OF REPO FOLDERS
            foreach (XmlNode tmpNode in section.SelectNodes("DataStore"))
            {
                //DETECT IF ITS AN ABSOLUTE PATH OF A PARTIAL PATH
                if (tmpNode.Attributes["Path"].Value.StartsWith("~"))
                    tmpRepoFolder = new RepositoryFolder(HttpContext.Current.Server.MapPath(tmpNode.Attributes["Path"].Value), tmpNode.Attributes["AccessMode"].Value.ToLower());
                else
                    tmpRepoFolder = new RepositoryFolder(tmpNode.Attributes["Path"].Value, tmpNode.Attributes["AccessMode"].Value);

                //ADD THE REPO FOLDER TO THE LIST OF FOLDERS RETURNING
                tmpReturn.mRepositoryFolders.Add(tmpRepoFolder);
            }

            //VALIDATE THE CONTENTS OF THE RETURN
            if (!ValidateConfig(tmpReturn))
                return null;
            else
                return tmpReturn;
        }

        private static bool ValidateConfig(RepositorySettings tmpReturn)
        {
#if !DEBUG
            //VERIFY THERE ARE NOT DUPLICATE REMOTE STORAGE LOCATIONS
            if ((from a in tmpReturn.RepositoryFolders group a by a.Path.ToLower()).Where(b => b.Count() > 1).Count() != 0)
                return false;
#endif
            //IF WE HAVE MADE IT THIS FAR THEN THE CONFIG FILE MUST BE OK
            return true;
        }

        public struct RepositoryFolder
        {
            public string Path;
            public string AccessMode;

            public RepositoryFolder(string path, string accessMode)
            {
                Path = path;
                AccessMode = accessMode.ToLower();
            }

            public override string ToString()
            {
                return Path;
            }
        }
    }

    public class RepositorySettingsHandler : IConfigurationSectionHandler
    {
        #region IConfigurationSectionHandler Members

        public object Create(object parent, object configContext, XmlNode section)
        {
            return RepositorySettings.LoadFromXml(section);
        }

        #endregion
    }

}