using Capsis.Handler;
using Capsis.Handler.Main;
using Capsis.Handler.Requests;
using static Capsis.Handler.CapsisProcessHandler;

namespace CapsisWebAPI
{
    /// <summary>
    /// This class is served as a static storage for common requests that do not necessarily require a process to run.
    /// It is created and populated at startup and then used for other requests than Simulate-related requests
    /// </summary>
    public class StaticQueryCache
    {
        /// <summary>
        /// Contain the data for a particular variant
        /// </summary>
        public class VariantData
        {
            public Dictionary<VariantSpecies.Type, List<string>> SpeciesMap { get; private set; }
            public List<ImportFieldElementIDCard> Fields { get; private set; }

            public List<string> Requests { get; private set; }

            public VariantData()
            {
                SpeciesMap = new Dictionary<VariantSpecies.Type, List<string>>();
                Fields = new List<ImportFieldElementIDCard>();
                Requests = new List<string>();      
            }

            private class VariantSpeciesData
            {
                public List<string> list;
            }

        }


        public Dictionary<Variant, VariantData> VariantDataMap { get; private set; }

        public StaticQueryCache()
        {
            VariantDataMap = new Dictionary<Variant, VariantData>();
        }


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
            logger.LogInformation("Initializing static query cache...");

            StaticQueryCache staticQueryCache = new();


            List<Variant> variantList = CapsisProcessHandler.GetVariantList();
            foreach (Variant variant in variantList)
            {
                CapsisProcessHandler handler = new(appSettings, logger, variant, refHandler : true);
                handler.Start();

                StaticQueryCache.VariantData data = new();

                // query variant species
                foreach (var speciesCode in Enum.GetValues<VariantSpecies.Type>())
                {
                    List<string> speciesList = handler.VariantSpecies(speciesCode);
                    data.SpeciesMap[speciesCode] = speciesList;
                }

                data.Fields.Clear();
                data.Fields.AddRange(handler.VariantFieldList());

                data.Requests.Clear();
                data.Requests.AddRange(handler.VariantRequests());

                staticQueryCache.VariantDataMap[variant] = data;

                handler.Stop();
                if (handler.ErrorMessage != null)
                {
                    logger.LogError("An error occurred while performing the static query: " + handler.ErrorMessage);
                    throw new Exception(handler.ErrorMessage);
                }
            }


            logger.LogInformation("Static query cache successfully initalized: " + staticQueryCache.ToString());
            return staticQueryCache;
        }

        public override string ToString()
        {
            return "Variants: " + VariantDataMap.ToString();
        }
    }
}
