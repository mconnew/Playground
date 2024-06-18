using System.Text;

namespace NetNamedPipePathSearcher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }

            Uri serviceUri;
            if (!Uri.TryCreate(args[0], UriKind.Absolute, out serviceUri))
            {
                Usage();
                return;
            }

            if (serviceUri.Scheme != Uri.UriSchemeNetPipe)
            {
                Usage();
                return;
            }

            var searchedEndpoints = PathSearcher.SearchPath(serviceUri);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(SearchedEndpoint.Header);
            foreach (var endpoint in searchedEndpoints)
            {
                sb.AppendLine(endpoint.ToString());
            }
            sb.AppendLine("# - Best match when using wcf:useBestMatchNamedPipeUri");
            sb.AppendLine("+ - First match");
            Console.WriteLine(sb.ToString());
        }

        private static void Usage()
        {
            string exeFileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string programName = Path.GetFileNameWithoutExtension(exeFileName);
            Console.WriteLine("Missing or invalid path. Usage:");
            Console.WriteLine($"\t{programName} net.pipe://localhost/servicePath/");
        }
    }
}
