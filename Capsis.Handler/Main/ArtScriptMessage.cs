using Capsis.Handler.Main;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capsis.Handler
{
    internal class ArtScriptMessage
    {
        public enum ArtScriptMessageType
        {
            ARTSCRIPT_MESSAGE_STATUS,
            ARTSCRIPT_MESSAGE_STOP,
            ARTSCRIPT_MESSAGE_SIMULATE,
            ARTSCRIPT_MESSAGE_GET_SPECIES_OF_TYPE,
            ARTSCRIPT_MESSAGE_GET_FIELDS,
            ARTSCRIPT_MESSAGE_ERROR,
            ARTSCRIPT_MESSAGE_COMPLETED,
        }

        enum ArtScriptSpeciesType
        {
            ConiferousSpecies,
            BroadleavedSpecies
        }

        public ArtScriptMessage(string message, string payload)
        {
            this.message = message;
            this.payload = payload;
        }

        public string message { set; get; }
        public string payload { set; get; }

        public static ArtScriptMessage CreateMessageStop()
        {
            return new ArtScriptMessage(Enum.GetName<ArtScriptMessageType>(ArtScriptMessageType.ARTSCRIPT_MESSAGE_STOP), null);
        }

        public static ArtScriptMessage CreateMessageSimulate(int initialDateYr, bool isStochastic, int nbRealizations, string applicationScale, string climateChange, int finalDateYr, int[] fieldMatches, string fileName)
        {
            var simParams = new Dictionary<string, dynamic>();
            simParams.Add("initialDateYr", initialDateYr);
            simParams.Add("isStochastic", isStochastic);
            simParams.Add("nbRealizations", nbRealizations);
            simParams.Add("applicationScale", applicationScale);
            simParams.Add("climateChange", climateChange);
            simParams.Add("finalDateYr", finalDateYr);
            simParams.Add("fieldMatches", fieldMatches);
            simParams.Add("fileName", fileName);

            string payload = JsonConvert.SerializeObject(simParams);
            return new ArtScriptMessage(Enum.GetName<ArtScriptMessageType>(ArtScriptMessageType.ARTSCRIPT_MESSAGE_SIMULATE), payload);
        }

        public static ArtScriptMessage CreateMessageGetSpeciesOfType(VariantSpecies.Type speciesType)
        {            
            string? species = speciesType == VariantSpecies.Type.All ? null : speciesType == VariantSpecies.Type.Coniferous ? Enum.GetName(ArtScriptSpeciesType.ConiferousSpecies) : Enum.GetName(ArtScriptSpeciesType.BroadleavedSpecies);

            return new ArtScriptMessage(Enum.GetName<ArtScriptMessageType>(ArtScriptMessageType.ARTSCRIPT_MESSAGE_GET_SPECIES_OF_TYPE), species);
        }

        public static ArtScriptMessage CreateMessageGetFieldList()
        {
            return new ArtScriptMessage(Enum.GetName<ArtScriptMessageType>(ArtScriptMessageType.ARTSCRIPT_MESSAGE_GET_FIELDS), null);
        }

        public static ArtScriptMessage CreateMessageStatus()
        {
            return new ArtScriptMessage(Enum.GetName<ArtScriptMessageType>(ArtScriptMessageType.ARTSCRIPT_MESSAGE_STATUS), null);
        }
    }
}
