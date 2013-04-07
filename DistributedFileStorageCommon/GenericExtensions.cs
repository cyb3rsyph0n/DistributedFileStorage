using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;

namespace DistributedFileStorageCommon
{
    public static class GenericExtensions
    {
        /// <summary>
        /// TURN AN OBJECT INTO AN XML REPRESENTATION
        /// </summary>
        /// <typeparam name="t">TYPE OF OBJECT TO SERIALIZE</typeparam>
        /// <param name="obj">INCOMING OBJECT TO BE SERIALIZED</param>
        /// <returns>STRING REPRESENTAION OF THE OBJECT</returns>
        public static string Serialize<t>(this t obj)
        {
            //MAKE SURE THE TYPE WHICH WAS PASSED IN IS SERIALIZABLE
            if(!typeof(t).IsSerializable)
                throw new ArgumentException("Invalid type specified for serialization, type must contain Serializeable Attribute");

            //IF THE TYPE CAN BE SERIALIZED THEN CREATE A MEMORY STREAM TO HOLD THE DATA
            using (MemoryStream tmpMem = new MemoryStream())
            {
                XmlSerializer tmpSeriazlie = new XmlSerializer(typeof(t));
                tmpSeriazlie.Serialize(tmpMem, obj);

                //RETURN THE XML RESULT OF SERIALIZING THE OBJECT
                return System.Text.ASCIIEncoding.ASCII.GetString(tmpMem.ToArray());
            }
        }

        /// <summary>
        /// TURN AN XML STRING INTO AN OBJECT REPRESENTATION
        /// </summary>
        /// <typeparam name="t">TYPE OF OBJECT REPRESENTED IN THE XML</typeparam>
        /// <param name="s">XML STRING OF THE OBJECT</param>
        /// <returns>DESERIALIZED OBJECT</returns>
        public static t Deserialize<t>(this string s)
        {
            //MAKE SURE THE TYPE WHICH WAS PASSED IN IS SERIALIZABLE
            if (!typeof(t).IsSerializable)
                throw new ArgumentException("Invalid type specified for deserialization, type must contain Serializeable Attribute");

            //IF THE TYPE CAN BE DESERIALIZED THEN CREATE A MEMORY STREAM HOLDING THE XML DATA
            using (MemoryStream tmpMem = new MemoryStream(System.Text.ASCIIEncoding.ASCII.GetBytes(s)))
            {
                XmlSerializer tmpSerialize = new XmlSerializer(typeof(t));

                //RETURN THE OBJECT RESULT OF  DESERIALIZING THE XML
                return (t)tmpSerialize.Deserialize(tmpMem);
            }
        }
    }
}
