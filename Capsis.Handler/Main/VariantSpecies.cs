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
namespace Capsis.Handler.Main
{
    public class VariantSpecies
    {
        public enum Type
        {
            All,
            Coniferous,
            Broadleaved
        }

        public string key { get; set; }
        public string commonName { get; set; }

        static private List<string> FVSConiferousSpecies = new List<string> { "PW", "LW", "FD", "BG", "HW", "CW", "PL", "SE", "BL", "PY", "OC" };

        public static bool typeIncludesConiferous(Type type)
        {
            return (type == Capsis.Handler.Main.VariantSpecies.Type.All || type == Capsis.Handler.Main.VariantSpecies.Type.Coniferous);
        }

        public static bool typeIncludesBroadleaved(Type type)
        {
            return (type == Capsis.Handler.Main.VariantSpecies.Type.All || type == Capsis.Handler.Main.VariantSpecies.Type.Broadleaved);
        }

        //public static bool isFVSSpeciesConiferous(FVSAPI.Species species)
        //{
        //    return FVSConiferousSpecies.Contains(species.fvs_code);
        //}
    }
}
