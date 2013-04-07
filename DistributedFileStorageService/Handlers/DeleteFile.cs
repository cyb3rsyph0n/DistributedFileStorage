using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Configuration;
using DistributedFileStorageService.DBAccess;
using System.Diagnostics;

namespace DistributedFileStorageService.Handlers
{
    public class DeleteFile : IHttpHandler
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
            bool deletedFile = false;

            //ATTEMPT TO STORE THE FILE IN ALL THE REPOSITORIES UNTIL ONE SUCCEEDS
            foreach (string tmpFolder in (from a in ((RepositorySettings)ConfigurationManager.GetSection("RepositorySettings")).RepositoryFolders where a.AccessMode.Contains("rw") select a.Path))
            {
                //FILE IS STORED IN THE COMBINATION OF THE LOCAL REPO FOLDER + FOLDER NAME PASSED IN
                string fileFolder = tmpFolder + Server.UrlDecode(Request.QueryString["folder"].Replace("/", "\\"));
                string fileName = Path.Combine(fileFolder, Server.UrlDecode(Request.QueryString["filename"]));

                if (File.Exists(fileName))
                {
                    try
                    {
                        //TRY TO DELETE THE FILE
                        File.Delete(fileName);

                        //IF WE HAVE MADE IT THIS FAR THEN STOP PROCESSING
                        deletedFile = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        Logging.WriteEntry(e.Message, e, new StackTrace());
                    }
                }
                else
                {
                    deletedFile = true;
                }
            }

            //CHECK TO SEE IF THE FILE WAS DELETED AND IF SO THEN RETURN SUCCESS OTHERWISE RETURN FAILURE
            if (deletedFile)
                Response.Write("SUCCESS");
            else
                Response.Write("FAILURE");
        }

        #endregion
    }
}
