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
        public int TimeoutSeconds { get; private set; }
        public int TimeoutSecondsRefHandler { get; private set; }

        /// <summary>
        /// Provide the settings for the CapsisProcessHandler class.
        /// <para> The settings contain the CAPSIS directory path, the DATA
        /// directory path and the timeout parameter (ms). If the folders ./capsis and
        /// ./data exist, they are automatically selected regardless of what is 
        /// specified in the json file.</para>
        /// </summary>
        /// <param name="jsonFilename"></param>
        /// <exception cref="ArgumentException"></exception>
        public CapsisProcessHandlerSettings(string jsonFilename)
        {
            IConfigurationRoot cb = new ConfigurationBuilder().AddJsonFile(jsonFilename).Build();
            string rootPath = Environment.CurrentDirectory;
            string potentialCapsisPath = Path.Combine(rootPath, "capsis");
            CapsisDirectory = Directory.Exists(potentialCapsisPath) ? potentialCapsisPath : cb["CapsisPath"];
            if (!Directory.Exists(CapsisDirectory))
                throw new ArgumentException("The CAPSIS folder: " + CapsisDirectory + " does not exist!");
            string potentialDataPath = Path.Combine(rootPath, "data");
            DataDirectory = Directory.Exists(potentialDataPath) ? potentialDataPath : cb["DataDirectory"];
            if (!Directory.Exists(DataDirectory))
                throw new ArgumentException("The DATA folder: " + DataDirectory + " does not exist!");
            TimeoutSeconds = GetSeconds(cb, "TimeoutSec");
            TimeoutSecondsRefHandler = GetSeconds(cb, "TimeoutSecRefHandler");
        }


        private static int GetSeconds(IConfigurationRoot cb, string slotname)
        {
            int timeoutSec = int.Parse(cb[slotname]);
            if (timeoutSec < 0)
                throw new ArgumentException($"The {slotname} parameter in the JSON configuration file should be positive (e.g. >= 0)!");
            return timeoutSec;
        }
    }
}
