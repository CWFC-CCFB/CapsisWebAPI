using Capsis.Handler;
using Capsis.Handler.Main;
using Capsis.Handler.Requests;

namespace CapsisWebAPI
{
    /// <summary>
    /// This class is served as a static storage for common requests that do not necessarily require a process to run.
    /// It is created and populated at startup and then used for other requests than Simulate-related requests
    /// </summary>
    public class StaticQueryCache
    {
        public StaticQueryCache()
        {
            variantDataMap = new Dictionary<string, VariantData>();
        }
        public class VariantData
        {
            public VariantData()
            {
                speciesMap = new Dictionary<VariantSpecies.Type, List<string>>();
            }

            private class VariantSpeciesData
            {
                public List<string> list;
            }

            public Dictionary<VariantSpecies.Type, List<string>> speciesMap;
            public List<ImportFieldElementIDCard> fields;
        }

        public Dictionary<String, VariantData> variantDataMap;
        public List<RequestType> requestTypes;

        /// <summary>
        /// Fill the static cache using a temporary handler
        /// </summary>
        /// <param name="CapsisPath"> the path to CAPSIS app </param>
        /// <param name="DataDirectory"> the path to the data directory </param>
        /// <param name="logger"> the application logger </param>
        /// <param name="TimeoutMillisec"> the number of milliseconds before calling a timeout </param>
        /// <returns>The StaticQueryCache created</returns>
        public static StaticQueryCache FillStaticCache(AppSettings appSettings, ILogger logger)
        {
            StaticQueryCache staticQueryCache = new();

            CapsisProcessHandler handler = new(appSettings, logger);
            handler.Start();

            List<String> variantList = handler.VariantList();
            foreach (var variant in variantList)
            {
                StaticQueryCache.VariantData data = new StaticQueryCache.VariantData();

                // query variant species
                foreach (var speciesCode in Enum.GetValues<VariantSpecies.Type>())
                {
                    List<String> result = handler.VariantSpecies(variant, speciesCode);
                    data.speciesMap[speciesCode] = result;
                }

                data.fields = handler.VariantFieldList(variant);

                staticQueryCache.variantDataMap[variant] = data;
            }

            staticQueryCache.requestTypes = handler.OutputRequestTypes();

            handler.Stop();

            return staticQueryCache;
        }
    }
}
