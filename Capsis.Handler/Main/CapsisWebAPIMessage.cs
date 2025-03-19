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

namespace Capsis.Handler
{
    internal class CapsisWebAPIMessage
    {
        public enum CapsisWebAPIMessageType
        {
            MESSAGE_STATUS,
            MESSAGE_STOP,
            MESSAGE_SIMULATE,
            MESSAGE_SIMULATION_STARTED,
            MESSAGE_GET_SPECIES_OF_TYPE,
            MESSAGE_GET_FIELDS,
            MESSAGE_GET_REQUESTS,
            MESSAGE_GET_VERSION,
            MESSAGE_GET_POSSIBLE_MESSAGES,
            MESSAGE_ERROR,
            MESSAGE_COMPLETED,
            MESSAGE_PORT,
            MESSAGE_GET_SCOPE
        }

        enum ArtScriptSpeciesType
        {
            ConiferousSpecies,
            BroadleavedSpecies
        }

        public CapsisWebAPIMessage(string message, string payload)
        {
            this.message = message;
            this.payload = payload;
        }

        public string message { set; get; }
        public string payload { set; get; }


        internal static CapsisWebAPIMessage CreateMessageSimulate(List<OutputRequest>? outputRequestList, 
                                                                    int initialDateYr, 
                                                                    bool isStochastic, 
                                                                    int nbRealizations, 
                                                                    string applicationScale, 
                                                                    string climateChange, 
                                                                    int finalDateYr, 
                                                                    Dictionary<string, string> fieldMatches, 
                                                                    string fileName)
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
            if (outputRequestList != null)
            {
                simParams.Add("outputRequestList", outputRequestList);
            }

            string payload = JsonConvert.SerializeObject(simParams);
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_SIMULATE), payload);
        }

        internal static CapsisWebAPIMessage CreateMessageGetSpeciesOfType(VariantSpecies.Type speciesType)
        {            
            string? species = speciesType == VariantSpecies.Type.All ? null : speciesType == VariantSpecies.Type.Coniferous ? Enum.GetName(ArtScriptSpeciesType.ConiferousSpecies) : Enum.GetName(ArtScriptSpeciesType.BroadleavedSpecies);

            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_SPECIES_OF_TYPE), species);
        }

        internal static CapsisWebAPIMessage CreateMessageGetFieldList()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_FIELDS), null);
        }

        internal static CapsisWebAPIMessage CreateMessageGetRequestList()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_REQUESTS), null);
        }

        internal static CapsisWebAPIMessage CreateMessageGetScope()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_SCOPE), null);
        }

        internal static CapsisWebAPIMessage CreateMessageStatus()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_STATUS), null);
        }

        internal static CapsisWebAPIMessage CreateMessageStop()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_STOP), null);
        }

        internal static CapsisWebAPIMessage CreateMessageVersion()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_VERSION), null);
        }

        internal static CapsisWebAPIMessage CreateMessagPossibleMessages()
        {
            return new CapsisWebAPIMessage(Enum.GetName<CapsisWebAPIMessageType>(CapsisWebAPIMessageType.MESSAGE_GET_POSSIBLE_MESSAGES), null);
        }

    }
}
