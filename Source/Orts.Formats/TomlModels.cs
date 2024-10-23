using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Formats
{
    public class ValueUnit<value, unit>
    {
        public value Value { get; set; }
        public unit Unit { get; set; }
    }
}
