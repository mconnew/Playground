using Ry.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace BinaryXmlSerialization
{
    public class BinaryXmlSerializer<T>
    {
        private static DataContractSerializer s_dcs;
        private DataContractSerializer _dcs;
        private TrackingXmlBinaryWriterSession _xmlBinaryWriterSession;
        private XmlBinaryReaderSession _xmlBinaryReaderSession;
        private static IChunkPool s_chunkPool;
        private static object s_lock = new object();

        public BinaryXmlSerializer()
        {
            _dcs = new DataContractSerializer(typeof(T));
            _xmlBinaryWriterSession = new TrackingXmlBinaryWriterSession();
        }

        /// <summary>
        /// Resets the internal state of the serializer by clearing the writer session saved state and clearing
        /// a saved reader session if it exists..
        /// </summary>
        public void Reset()
        {
            _xmlBinaryWriterSession.ClearNew();
            _xmlBinaryReaderSession = null;
        }

        private static void EnsureDcs()
        {
            if (s_dcs == null)
            {
                s_dcs = new DataContractSerializer(typeof(T));
            }
        }

        private static void EnsureChunkPool()
        {
            if (s_chunkPool == null)
            {
                lock (s_lock)
                {
                    if (s_chunkPool == null)
                    {
                        s_chunkPool = new ChunkPool();
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously serializes an object to a stream.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="outputStream">The stream to write the serialized data to.</param>
        /// <param name="writeSessionData">Indicates whether to write session data to the output stream or save it for writing with the WriteSessionDataAsync method.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SerializeAsync(T obj, Stream outputStream, bool writeSessionData)
        {
            XmlDictionaryWriter xmlDictionaryWriter;
            ChunkedStream chunkedStream = null;
            try
            {
                if (writeSessionData)
                {
                    EnsureChunkPool();
                    chunkedStream = new ChunkedStream(s_chunkPool);
                    xmlDictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(chunkedStream, new XmlDictionary(), _xmlBinaryWriterSession, false);
                }
                else
                {
                    xmlDictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(outputStream, new XmlDictionary(), _xmlBinaryWriterSession, false);
                }

                _dcs.WriteObject(xmlDictionaryWriter, obj);
                xmlDictionaryWriter.Flush();
                xmlDictionaryWriter.Close();
                if (writeSessionData)
                {
                    if (_xmlBinaryWriterSession.HasNewStrings)
                    {
                        using (var bw = new BinaryWriter(outputStream, Encoding.UTF8, true))
                        {
                            foreach (var newString in _xmlBinaryWriterSession.NewStrings)
                            {
                                bw.Write(newString.Value);
                            }
                            bw.Write(string.Empty);
                        }

                        await outputStream.FlushAsync();
                    }

                    chunkedStream.Position = 0;
                    await chunkedStream.MoveToAsync(outputStream);
                    _xmlBinaryWriterSession.ClearNew();
                }

                await outputStream.FlushAsync();
            }
            finally
            {
                chunkedStream?.Dispose();
            }
        }

        /// <summary>
        /// Asynchronously writes saved session data to a stream. Passing writeSessionData = false to SerializeAsync will result in the session data being saved.
        /// </summary>
        /// <param name="outputStream">The stream to write the session data to.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether session data was written.</returns>
        public async Task<bool> WriteSessionDataAsync(Stream outputStream)
        {
            if (_xmlBinaryWriterSession.HasNewStrings)
            {
                using (var bw = new BinaryWriter(outputStream, Encoding.UTF8, true))
                {
                    foreach (var newString in _xmlBinaryWriterSession.NewStrings)
                    {
                        bw.Write(newString.Value);
                    }
                    bw.Write(string.Empty);
                }

                _xmlBinaryWriterSession.ClearNew();
                await outputStream.FlushAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Deserializes an object from a stream. If ReadSharedSessionData has previously been called, the shared session data that was read will be used with the binary XmlDictionaryReader.
        /// If ReadSharedSessionData has not been previously called, or Reset has been called, then this method will read the session data from the input stream.
        /// </summary>
        /// <param name="inputStream">The stream to read the serialized data from.</param>
        /// <returns>The deserialized object.</returns>
        public T Deserialize(Stream inputStream)
        {
            XmlBinaryReaderSession xmlBinaryReaderSession;
            if (_xmlBinaryReaderSession == null)
            {
                xmlBinaryReaderSession = ReadSessionData(inputStream);
            }
            else
            {
                xmlBinaryReaderSession = _xmlBinaryReaderSession;
            }

            var xmlDictionaryReader = XmlDictionaryReader.CreateBinaryReader(inputStream, new XmlDictionary(), XmlDictionaryReaderQuotas.Max, xmlBinaryReaderSession);
            return (T)_dcs.ReadObject(xmlDictionaryReader);
        }

        /// <summary>
        /// Reads shared session data from a stream and saves it for later use when calling Deserializer.
        /// </summary>
        /// <param name="inputStream">The stream to read the session data from.</param>
        public void ReadSharedSessionData(Stream inputStream)
        {
            _xmlBinaryReaderSession = ReadSessionData(inputStream);
        }

        /// <summary>
        /// Asynchronously serializes an object to a stream. This will save the session data in the output stream ahead of the serialized payload.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="outputStream">The stream to write the serialized data to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task SerializeAsync(T obj, Stream outputStream)
        {
            EnsureDcs();
            EnsureChunkPool();
            ChunkedStream chunkedStream = new ChunkedStream(s_chunkPool);
            try
            {
                var xmlBinaryWriterSession = new TrackingXmlBinaryWriterSession();
                var xmlDictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(chunkedStream, new XmlDictionary(), xmlBinaryWriterSession, false);
                s_dcs.WriteObject(xmlDictionaryWriter, obj);
                xmlDictionaryWriter.Flush();
                xmlDictionaryWriter.Close();
                if (xmlBinaryWriterSession.HasNewStrings)
                {
                    using (var bw = new BinaryWriter(outputStream, Encoding.UTF8, true))
                    {
                        foreach (var newString in xmlBinaryWriterSession.NewStrings)
                        {
                            bw.Write(newString.Value);
                        }

                        bw.Write(string.Empty);
                    }

                    await outputStream.FlushAsync();
                }

                chunkedStream.Position = 0;
                await chunkedStream.MoveToAsync(outputStream);
                await outputStream.FlushAsync();
            }
            finally
            {
                chunkedStream?.Dispose();
            }
        }

        /// <summary>
        /// Deserializes an object from a stream using the provided session data. If sessionData is null, it will expect the session data to exist at the start of the input stream.
        /// </summary>
        /// <param name="inputStream">The stream to read the serialized data from.</param>
        /// <param name="sessionData">The session data to use for deserialization.</param>
        /// <returns>The deserialized object.</returns>
        public static T Deserialize(Stream inputStream, XmlBinaryReaderSession sessionData)
        {
            EnsureDcs();
            if (sessionData == null)
            {
                sessionData = ReadSessionData(inputStream);
            }
            var xmlDictionaryReader = XmlDictionaryReader.CreateBinaryReader(inputStream, new XmlDictionary(), XmlDictionaryReaderQuotas.Max, sessionData);
            return (T)s_dcs.ReadObject(xmlDictionaryReader);
        }

        /// <summary>
        /// Reads session data from a stream.
        /// </summary>
        /// <param name="inputStream">The stream to read the session data from.</param>
        /// <returns>The session data.</returns>
        public static XmlBinaryReaderSession ReadSessionData(Stream inputStream)
        {
            var xmlBinaryReaderSession = new XmlBinaryReaderSession();
            using (var br = new BinaryReader(inputStream, Encoding.UTF8, true))
            {
                int dictionaryId = 0;
                while (true)
                {
                    var str = br.ReadString();
                    if (string.IsNullOrEmpty(str))
                    {
                        break;
                    }

                    xmlBinaryReaderSession.Add(dictionaryId, str);
                    dictionaryId++;
                }
            }
            return xmlBinaryReaderSession;
        }
    }
}
