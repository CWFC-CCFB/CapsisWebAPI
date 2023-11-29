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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
