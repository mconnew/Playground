using System;
using System.Net;
using System.Threading;

namespace TrackHttpPayload
{
    internal class HttpWebRequestProxyCreator : IWebRequestCreate
    {
        private readonly IHttpWebRequestInspector _callbackHandler;

        public HttpWebRequestProxyCreator(IHttpWebRequestInspector callbackHandler)
        {
            _callbackHandler = callbackHandler;
        }

        public static void RegisterPrefix(string prefix, IHttpWebRequestInspector callbackHandler)
        {
            WebRequest.RegisterPrefix(prefix, new HttpWebRequestProxyCreator(callbackHandler));
        }

        public WebRequest Create(Uri uri)
        {
            return (HttpWebRequest)(new HttpWebRequestProxy(uri, _callbackHandler)).GetTransparentProxy();
        }
    }

    internal class HttpWebRequestBalancedCreator : IWebRequestCreate
    {
        private static long s_counter = 0;
        private int _blanceFactor;

        public HttpWebRequestBalancedCreator(int balanceFactor)
        {
            _blanceFactor = balanceFactor;
        }

        public WebRequest Create(Uri uri)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.CreateDefault(uri);
            httpWebRequest.ConnectionGroupName = "BalancedConnectionGroup" + (Interlocked.Increment(ref s_counter) % _blanceFactor);
            return httpWebRequest;
        }

        public static void BalanceUrlPrefix(string prefix, int balanceFactor)
        {
            WebRequest.RegisterPrefix(prefix, new HttpWebRequestBalancedCreator(balanceFactor));
        }
    }
}