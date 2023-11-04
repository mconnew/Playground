using CoreWCF;
using System;
using System.Runtime.Serialization;

namespace ConfigureServiceByEndpoint
{
    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        string GetData(int value);
    }

    public class Service : IService
    {
        private ILogger? _logger;
        private string? _prefix;

        public Service(ILogger logger, string prefix) 
        { 
            _logger = logger;
            _prefix = prefix;
        }

        internal Service() { }

        public string GetData(int value)
        {
            _logger?.LogInformation("You entered: {value}", value);
            return $"[{_prefix}] You entered: {value}";
        }
    }
}
