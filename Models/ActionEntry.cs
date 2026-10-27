namespace PAoC_Mod.Models
{
    public class ActionEntry
    {
        public int CountryId { get; set; }
        public string Command { get; set; }
        public int? TargetId { get; set; }
        public int? SecondaryTargetId { get; set; }
        public string TextValue { get; set; }
        public string SecondaryTextValue { get; set; }
        public string Mode { get; set; }
        public int? Amount { get; set; }
    }
}