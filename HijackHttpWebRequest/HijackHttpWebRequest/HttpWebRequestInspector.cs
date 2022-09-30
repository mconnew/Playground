using System;
using System.Net;

namespace TrackHttpPayload
{
    public static class HttpWebRequestInspector
    {
        public static IHttpWebRequestInspector InspectRequests(Uri baseUri)
        {
            var callbackHandler = new HttpWebRequestInspectorImpl();
            HttpWebRequestProxyCreator.RegisterPrefix(baseUri.ToString(), callbackHandler);
            return callbackHandler;
        }

        internal class HttpWebRequestInspectorImpl : IHttpWebRequestInspector
        {
            public SendRequestCallback SendingRequest { get; set; }
            public GetResponseCallback ReceivingResponse { get; set; }
        }
    }



    public interface IHttpWebRequestInspector
    {
        SendRequestCallback SendingRequest { get; set; }
        GetResponseCallback ReceivingResponse { get; set; }
    }

    public delegate void SendRequestCallback(HttpWebRequest httpWebRequest);

    public delegate void GetResponseCallback(HttpWebResponse httpWebResponse);
}