using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.IO;
using DistributedFileStorageService.DBAccess;
using System.Diagnostics;

namespace DistributedFileStorageService.Handlers
{
    public class GetFile : IHttpHandler
    {
        #region IHttpHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            HttpServerUtility Server = context.Server;
            HttpRequest Request = context.Request;
            HttpResponse Response = context.Response;
            bool sentFile = false;

            //ATTEMPT TO STORE THE FILE IN ALL THE REPOSITORIES UNTIL ONE SUCCEEDS
            foreach (string tmpFolder in (from a in ((RepositorySettings)ConfigurationManager.GetSection("RepositorySettings")).RepositoryFolders where a.AccessMode.Contains("r") select a.Path))
            {
                //FILE IS STORED IN THE COMBINATION OF THE LOCAL REPO FOLDER + FOLDER NAME PASSED IN
                string fileFolder = tmpFolder + Server.UrlDecode(Request.QueryString["folder"].Replace("/", "\\"));
                string fileName = Path.Combine(fileFolder, Server.UrlDecode(Request.QueryString["filename"]));

                //IF THE FILE EXISTS THEN TRANSMIT IT BACK AND EXIT OTHERWISE WE WILL TRY THE OTHER REPOSITORIES
                if(File.Exists(fileName))
                {
                    try
                    {
                        Response.TransmitFile(fileName);
                        sentFile = true;

                        break;
                    }
                    catch(Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace());
                    }
                }
            }

            //IF WE DID NOT SEND A FILE THEN THROW AN EXCEPTION
            if (!sentFile)
                throw new Exception("File Not Found");
        }

        #endregion
    }
}
