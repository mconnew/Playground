using System.ServiceModel;

namespace NetNamedPipePathSearcher
{
    public class SearchedEndpoint
    {
        private static int s_sharedMemoryNameMaxLength = 0;
        private static int s_pathMaxLength = 0;
        private static int s_hostNameMaxLength = 0;
        private static int s_comparisonModeMaxLength = 16;
        private static int s_globalMaxLength = 6;
        private static int s_sharedMemoryFoundMaxLength = 7;
        private static int s_pipeNameMaxLength = 0;
        private string sharedMemoryName = string.Empty;
        private string path = string.Empty;
        private string hostName = string.Empty;
        private string pipeName = "N/A";

        public string SharedMemoryName
        {
            get => sharedMemoryName; 
            internal set
            {
                sharedMemoryName = value;
                s_sharedMemoryNameMaxLength = System.Math.Max(s_sharedMemoryNameMaxLength, sharedMemoryName.Length);
            }
        }

        public string Path
        {
            get => path; 
            internal set
            {
                path = value;
                s_pathMaxLength = System.Math.Max(s_pathMaxLength, path.Length);
            }
        }

        public string HostName
        {
            get => hostName; 
            internal set
            {
                hostName = value;
                s_hostNameMaxLength = System.Math.Max(s_hostNameMaxLength, hostName.Length);
            }
        }

        public HostNameComparisonMode HostNameComparisonMode { get; internal set; }
        public bool UseGlobal { get; internal set; }
        public bool SharedMemoryFound { get; internal set; }

        public string PipeName
        {
            get => pipeName; 
            internal set
            {
                pipeName = value;
                s_pipeNameMaxLength = System.Math.Max(s_pipeNameMaxLength, pipeName.Length);
            }
        }

        public bool IsBestMatch { get; internal set; }
        public bool IsFirstMatch { get; internal set; }
        private string BestOrFirstPrefix => IsBestMatch && IsFirstMatch ? "#+" : IsBestMatch ? "# " : IsFirstMatch ? "+ " : "  ";

        public override string ToString()
        {
            return $"{BestOrFirstPrefix} {HostName.PadRight(s_hostNameMaxLength)} {HostNameComparisonMode.ToString().PadRight(s_comparisonModeMaxLength)} {Path.PadRight(s_pathMaxLength)} {UseGlobal.ToString().PadRight(s_globalMaxLength)} {SharedMemoryName.PadRight(s_sharedMemoryNameMaxLength)} {SharedMemoryFound.ToString().PadRight(s_sharedMemoryFoundMaxLength)} {PipeName.PadRight(s_pipeNameMaxLength)}";
        }

        public static string Header => $"   {"HostName".PadRight(s_hostNameMaxLength)} {"ComparisonMode".PadRight(s_comparisonModeMaxLength)} {"Path".PadRight(s_pathMaxLength)} {"Global".PadRight(s_globalMaxLength)} {"SharedMemoryName".PadRight(s_sharedMemoryNameMaxLength)} {"SMFound".PadRight(s_sharedMemoryFoundMaxLength)} {"PipeName".PadRight(s_pipeNameMaxLength)}";
    }
}
