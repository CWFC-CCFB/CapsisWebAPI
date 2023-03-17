using Newtonsoft.Json;

namespace Capsis.Handler.Requests
{
    public class OutputRequest
    {
        public OutputRequest(RequestType _requestType, Dictionary<string, List<string>>? _aggregationPatterns)
        {
            requestType = _requestType;
            aggregationPatterns = _aggregationPatterns;
        }

        [JsonRequired]
        public RequestType requestType { get; set; }

        public Dictionary<string, List<string>>? aggregationPatterns { get; set; }
    }
}
