using System.Collections.Generic;
using PAoC_Mod.Models;

namespace PAoC_Mod.Models
{
    public class DelayEventData
    {
        public string Name;
        public int Days;
        public int Months;
        public int Years;

        public int RandomMinDays;
        public int RandomMaxDays;
        public int RandomMinMonths;
        public int RandomMaxMonths;
        public int RandomMinYears;
        public int RandomMaxYears;

        public List<ScheduleNode> Effect;
    }
}