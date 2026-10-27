using PAoC_Mod.Models;
using System;
using System.Collections.Generic;

namespace PAoC_Mod.Models
{
    public class ConditionalEventData
    {
        public string Name;
        public List<ConditionCheck> Conditions = new List<ConditionCheck>();
        public Dictionary<int, List<ConditionCheck>> CountryConditions = new Dictionary<int, List<ConditionCheck>>();
        public List<ScheduleNode> Effect;
        public ConditionOperator Op = ConditionOperator.All;
        public int Value;
        public int? CountryId;
    }
}