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

        public Dictionary<string, VariantData> variantDataMap;
        public List<RequestType> requestTypes;

        /// <summary>
        /// Fills the static cache from a temporary handler
        /// </summary>
        /// <param name="CapsisPath"></param>
        /// <param name="DataDirectory"></param>
        /// <returns>The StaticQueryCache created</returns>
        public static StaticQueryCache FillStaticCache(string CapsisPath, string DataDirectory)
        {
            StaticQueryCache staticQueryCache = new StaticQueryCache();

            CapsisProcessHandler handler = new(CapsisPath, DataDirectory);
            handler.Start();

            List<string> variantList = handler.VariantList();
            foreach (var variant in variantList)
            {
                StaticQueryCache.VariantData data = new StaticQueryCache.VariantData();

                // query variant species
                foreach (var speciesCode in Enum.GetValues<VariantSpecies.Type>())
                {
                    List<string> result = handler.VariantSpecies(variant, speciesCode);
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
