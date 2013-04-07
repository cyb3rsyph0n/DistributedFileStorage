using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using System.Configuration;
using DistributedFileStorageCommon;
using System.Text;

namespace DistributedFileStorageService.DBAccess
{
    public static class Logging
    {
        /// <summary>
        /// WRITE AN ENTRY TO THE LOG FILE TRACKIGN AS MUCH AS WE POSSIBLY CAN
        /// </summary>
        /// <param name="errorText">BRIEF MESSAGE ABOUT THE ERROR CAN BE CUSTOM OR EXCEPTION MESSAGE</param>
        /// <param name="e">ACTUAL EXCEPTION WHICH WAS CAUGHT IF THERE IS ONE</param>
        /// <param name="stackTrace">CURRENTLY NOT USED BUT MIGHT BE FOR BETTER TRACKING</param>
        public static void WriteEntry(string errorText, Exception e, StackTrace stackTrace)
        {
            //CREATE A NEW LOG ENTRY FOR STORING IN THE DATABASE
            LogEntry tmpEntry = new LogEntry() { RecordID = Guid.NewGuid().ToString(), ErrorDate = DateTime.Now, ErrorText = errorText, Exception = e != null ? e.ToString() : null };

            //WRITE THE ENTRY TO EVERY DATABASE IN THE LIST OF DATABASES
            foreach (ConnectionStringSettings tmpConnection in ConfigurationManager.ConnectionStrings)
            {
                //CREATE THE TEMP CONNECTION TO THE DATABASE AND STORE THE ENTRY IN IT
                using (DistributedFileStorageDBDataContext tmpDB = new DistributedFileStorageDBDataContext(tmpConnection.ConnectionString))
                {
                    //INSERT AND SUBMIT THE CHANGES TO THE DATABASE
                    tmpDB.LogEntries.InsertOnSubmit(tmpEntry);
                    tmpDB.SubmitChanges();
                }
            }
        }
    }
}
