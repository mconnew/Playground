using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TrackHttpPayload.ServiceReference1;

namespace TrackHttpPayload
{
    class Program
    {
        static void Main(string[] args)
        {
            var inspector = HttpWebRequestInspector.InspectRequests(new Uri("http://localhost:10123/Service1.svc"));
            inspector.SendingRequest += request =>
            {
                Console.WriteLine("Request headers:");
                WebHeaderCollection headers = request.Headers;
                foreach (var headerName in headers.AllKeys)
                {
                    Console.WriteLine($"{headerName} = {headers[headerName]}");
                }

                Console.WriteLine();
            };

            inspector.ReceivingResponse += webResponse =>
            {
                Console.WriteLine("Response headers:");
                WebHeaderCollection headers = webResponse.Headers;
                foreach (var headerName in headers.AllKeys)
                {
                    Console.WriteLine($"{headerName} = {headers[headerName]}");
                }

                Console.WriteLine();
            };

            var proxy = new Service1Client();
            var response = proxy.GetData(32);
            Console.WriteLine(response);
            Console.ReadLine();
        }
    }
}
