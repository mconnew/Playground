using System.ServiceModel;

namespace BasicNamedPipeService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var host = new ServiceHost(typeof(Service), new Uri("net.pipe://localhost/MyService/")))
            {
                host.AddServiceEndpoint(typeof(IService), new NetNamedPipeBinding(), "pipe");
                host.Open();
                Console.WriteLine("Service started, hit any key");
                Console.ReadKey();
            }
        }
    }
}
