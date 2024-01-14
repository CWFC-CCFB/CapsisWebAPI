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
using Capsis.Handler.Main;
using Capsis.Handler.Requests;
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
            ARTSCRIPT_MESSAGE_SIMULATION_STARTED,
            ARTSCRIPT_MESSAGE_GET_SPECIES_OF_TYPE,
            ARTSCRIPT_MESSAGE_GET_FIELDS,
            ARTSCRIPT_MESSAGE_ERROR,
            ARTSCRIPT_MESSAGE_COMPLETED,
            ARTSCRIPT_MESSAGE_PORT
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

        public static ArtScriptMessage CreateMessageSimulate(List<OutputRequest>? outputRequestList, int initialDateYr, bool isStochastic, int nbRealizations, string applicationScale, string climateChange, int finalDateYr, int[] fieldMatches, string fileName)
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
            simParams.Add("outputRequestList", outputRequestList);

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
