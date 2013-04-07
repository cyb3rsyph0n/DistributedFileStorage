using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DistributedFileStorageCommon
{
    class TempFileStream : FileStream
    {
        /// <summary>
        /// STORE THE NAME OF THE FILE WHICH WE OPENED SO WE CAN DELETE IT WHEN THE STREAM IS CLOSED
        /// </summary>
        private string mFileName = null;

        public TempFileStream(string path, FileMode mode, FileAccess access, FileShare share)
            : base(path, mode, access, share)
        {
            //REMEMBER THE NAME OF THE FILE SO WE CAN DELETE IT LATER
            mFileName = path;
        }

        public override void Close()
        {
            base.Close();

            //ATTEMPT TO DELETE THE TEMP FILE
            if (mFileName != null)
                try
                {
                    File.Delete(mFileName);
                    mFileName = null;
                }
                catch
                {
                }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            //ATTEMPT TO DELETE THE TEMP FILE
            if (disposing)
                if (mFileName != null)
                    try
                    {
                        File.Delete(mFileName);
                        mFileName = null;
                    }
                    catch
                    {
                    }
        }
    }
}
