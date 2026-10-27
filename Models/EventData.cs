using System.Collections.Generic;

namespace PAoC_Mod.Models
{
    public class SuperEventData
    {
        public string ImageFileName;
        public string Title;
        public string Quote;
        public string QuoteAuthor;
        public string ButtonText;
        public string SoundFileName;
        public List<ScheduleNode> WhenActivated;
    }

    public class NewsData
    {
        public string ImageFileName;
        public string Title;
        public string Description;
        public string ButtonText;
        public List<ScheduleNode> Effect;
        public List<ScheduleNode> WhenActivated;
    }

    public class EventOption
    {
        public string Name;
        public List<ScheduleNode> Effect;
        public List<ConditionCheck> AvailableConditions;
        public ConditionOperator? AvailableOp;
        public int AvailableValue;
    }

    public class EventData
    {
        public string ImageFileName;
        public string Title;
        public string Description;
        public List<EventOption> Options = new List<EventOption>();
        public bool CloseButtonEnabled = true;
    }
}