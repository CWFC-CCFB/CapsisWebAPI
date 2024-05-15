/*
 * This file is part of the CapsisWebAPI solution
 *
 * Author Jean-Francois Lavoie - Canadian Forest Service
 * Copyright (C) 2023 His Majesty the King in Right of Canada
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied
 * warranty of MERCHANTABILITY or FITNESS FOR A
 * PARTICULAR PURPOSE. See the GNU Lesser General Public
 * License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */
using Capsis.Handler;
using Capsis.Handler.Main;
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

            public Dictionary<string, object> Scope { get; private set; }   

            public VariantData()
            {
                SpeciesMap = new Dictionary<VariantSpecies.Type, List<string>>();
                Fields = new List<ImportFieldElementIDCard>();
                Requests = new List<string>();      
                Scope = new Dictionary<string, object>();
            }

            private class VariantSpeciesData
            {
                public List<string> list;
            }

        }


        public Dictionary<Variant, VariantData> VariantDataMap { get; private set; }

        public List<string> PossibleMessages { get; private set; }

        public string CAPSISVersion { get; private set; }

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

                data.Scope.Clear();
                foreach (var scopeElement in handler.GetScope())
                    data.Scope.Add(scopeElement.Key, scopeElement.Value);

                staticQueryCache.VariantDataMap[variant] = data;

                staticQueryCache.CAPSISVersion = handler.RetrieveCAPSISVersion();
                staticQueryCache.PossibleMessages = handler.RetrievePossibleMessages();

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
