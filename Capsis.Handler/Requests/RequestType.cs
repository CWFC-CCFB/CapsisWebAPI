using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Linq;

namespace Capsis.Handler.Requests
{
    public class RequestType
    {
        public static List<RequestType> requestTypeList = new();

        public static readonly RequestType AliveVolume = new(StatusClass.Alive, Variable.Volume);        

        public enum StatusClass { Alive, Dead };
        public enum Variable { Volume };

        public string id
        {
            get
            {
                return statusClass.ToString() + variable.ToString();
            }
        }

        [JsonRequired]
        [JsonConverter(typeof(StringEnumConverter))]
        public StatusClass statusClass { get; set; }

        [JsonRequired]
        [JsonConverter(typeof(StringEnumConverter))]
        public Variable variable { get; set; }

        public RequestType(StatusClass sc, Variable v)
        {
            statusClass = sc;
            variable = v;

            if (!requestTypeList.Any(x => x.id == id))
                requestTypeList.Add(this);
        }
    }
}
