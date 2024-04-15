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

namespace CapsisWebAPI
{
    public class AppSettings : CapsisProcessHandlerSettings
    {

        private static readonly AppSettings _instance = new();

        private AppSettings() : base("appsettings.json")
        {




        } 

        /// <summary>
        /// Provide access to the singleton of AppSettings instance
        /// </summary>
        /// <returns> the AppSettings singleton </returns>
        public static AppSettings GetInstance()
        {
            return _instance;
        }

    }
}
