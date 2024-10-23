using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Formats
{
    public static class MpS
    {
        public static float From(float speed, string unit)
        {
            switch (unit)
            {
                case "m/s": return speed;
                case "km/h": return ORTS.Common.MpS.FromKpH(speed);
                case "mph": return ORTS.Common.MpS.FromMpH(speed);
                default: return speed;
            }
        }
    }
}
