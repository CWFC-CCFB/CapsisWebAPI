
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
