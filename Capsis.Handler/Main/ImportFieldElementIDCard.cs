using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capsis.Handler.Main
{
    public class ImportFieldElementIDCard
    {
        public bool isOptional { get; set; }
		public string name  { get; set; }
        public string type { get; set; }
        public int match { get; set; }
    }
}
