using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capsis.Handler
{
    public class ScriptResult
    {
        public DataSet dataset { get; set; }
	    public List<string> outputTypes { get; set; }
        public int nbRealizations { get; set; }
        public int nbPlots { get; set; }
        public string climateChangeScenario { get; set; }
        public string growthModel { get; set; }
    }
}
