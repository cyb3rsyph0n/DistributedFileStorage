using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Services.Protocols;
using System.IO;

namespace DistributedFileStorageCommon
{
    /// <summary>
    /// SOAP EXTENSION FOR COMPRESSING AND DECOMPRESSING INCOMING AND OUTGOING MESSAGES TO BE USED ON BOTH THE CLIENT AND WEBSERVICE
    /// </summary>
    public class SoapCompressor : SoapExtension
    {
        private Stream networkStream;
        private Stream newStream;

        public override object GetInitializer(Type serviceType)
        {
            return null;
        }

        public override object GetInitializer(LogicalMethodInfo methodInfo, SoapExtensionAttribute attribute)
        {
            return null;
        }

        public override void Initialize(object initializer)
        {
        }

        /// <summary>
        /// CHAINS THE INCOMING STREAM SO WE CAN MANIPULATE THE DATA BEHIND THE SCENES
        /// </summary>
        /// <param name="stream">INCOMING STREAM WHICH IS A NETWORK STREAM, SOMETIMES ITS INCOMING AND OTHERS ITS OUTGOING</param>
        /// <returns></returns>
        public override System.IO.Stream ChainStream(System.IO.Stream stream)
        {
            networkStream = stream;
            newStream = new MemoryStream();
            return newStream;
        }

        /// <summary>
        /// PROCESS THE SOAP MESSAGE
        /// </summary>
        /// <param name="message">SOAP MESSAGE EITHER INBOUND OR OUTBOUND</param>
        public override void ProcessMessage(SoapMessage message)
        {
            switch (message.Stage)
            {
                case SoapMessageStage.BeforeSerialize:
                    break;
                case SoapMessageStage.AfterSerialize:
                    CompressMessage();
                    break;
                case SoapMessageStage.BeforeDeserialize:
                    DeCompressMessage();
                    break;
                case SoapMessageStage.AfterDeserialize:
                    break;
            }
        }

        /// <summary>
        /// COMPRESS THE OUTGOING STREAM
        /// </summary>
        private void CompressMessage()
        {
            newStream.Seek(0, SeekOrigin.Begin);
            newStream.Compress(networkStream);
        }

        /// <summary>
        /// UNCOMPRESS THE INCOMING STREAM
        /// </summary>
        private void DeCompressMessage()
        {
            networkStream.Uncompress(newStream);
            newStream.Seek(0, SeekOrigin.Begin);
        }
    }
}

public class SoapCompressorAttribute : SoapExtensionAttribute
{
    public override Type ExtensionType
    {
        get { return typeof(DistributedFileStorageCommon.SoapCompressor); }
    }

    public override int Priority { get; set; }
}