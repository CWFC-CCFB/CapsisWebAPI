/*
 * This file is part of the CapsisWebAPI solution
 *
 * Author Mathieu Fortin - Canadian Forest Service
 * Copyright (C) 2023 His Majesty the King in right of Canada
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
using Microsoft.Extensions.Configuration;

namespace Capsis.Handler.Main
{
    public class CapsisProcessHandlerSettings
    {

        public string CapsisDirectory { get; private set; }
        public string DataDirectory { get; private set; }
        public int TimeoutMilliseconds { get; private set; }

        public CapsisProcessHandlerSettings(string jsonFilename)
        {
            IConfigurationRoot cb = new ConfigurationBuilder().AddJsonFile(jsonFilename).Build();
            CapsisDirectory = cb["CapsisPath"];
            DataDirectory = cb["DataDirectory"];
            TimeoutMilliseconds = int.Parse(cb["TimeoutMillisec"]);
        }
    }
}
