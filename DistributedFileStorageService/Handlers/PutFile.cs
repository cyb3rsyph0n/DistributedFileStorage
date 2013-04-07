using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Configuration;
using DistributedFileStorageService.DBAccess;

namespace DistributedFileStorageService.Handlers
{
    public class PutFile : IHttpHandler
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
            bool savedFile = false;

            //ATTEMPT TO STORE THE FILE IN ALL THE REPOSITORIES UNTIL ONE SUCCEEDS
            foreach (string tmpFolder in (from a in ((RepositorySettings)ConfigurationManager.GetSection("RepositorySettings")).RepositoryFolders where a.AccessMode.Contains("w") select a.Path))
            {
                //FILE IS STORED IN THE COMBINATION OF THE LOCAL REPO FOLDER + FOLDER NAME PASSED IN
                string fileFolder = tmpFolder + Server.UrlDecode(Request.QueryString["folder"].Replace("/", "\\"));
                string fileName = Path.Combine(fileFolder, Server.UrlDecode(Request.QueryString["filename"]));
                
                //CREATE THE DIRECTORY AND ATTEMPT TO STORE THE FILE IF IT SUCCEEDS THEN BREAK AND RETURN TRUE OTHERWISE GO TO THE NEXT ONE
                try
                {
                    //CREATE THE DIRECTORY WHERE THE FILE IS TO BE STORED
                    Directory.CreateDirectory(fileFolder);

                    //SAVE THE FILE
                    Request.Files[0].SaveAs(fileName);

                    //STORE THE SUCCESS FOR RETURN VALUE LATER
                    savedFile = true;

                    //BREAK THE LOOP SO WE DO NOT STICK IT IN THE NEXT REPOSITORY
                    break;
                }
                catch(Exception e)
                {
                    //LOG THE REASON WE COULD NOT STORE THE FILE
                    Logging.WriteEntry(e.Message, e, new System.Diagnostics.StackTrace());
                }
            }

            //IF WE SAVED THE FILE THEN RETURN SUCCESS OTHERWISE RETURN FAILURE
            if (savedFile)
                Response.Write("SUCCESS");
            else
                Response.Write("FAILURE");
        }

        #endregion
    }
}