using System.Collections.Generic;

namespace PAoC_Mod.Models
{
    public abstract class ScheduleNode
    {
    }

    public class CountryNode : ScheduleNode
    {
        public int CountryId;
        public List<ScheduleNode> Children;
    }

    public class ActionNode : ScheduleNode
    {
        public ActionEntry Action;
    }

    public class ChanceNode : ScheduleNode
    {
        public int Percent;
        public List<ScheduleNode> Children;
    }

    public class RandomOption
    {
        public int Weight;
        public List<ScheduleNode> Children;
    }

    public class RandomNode : ScheduleNode
    {
        public List<RandomOption> Options = new List<RandomOption>();
    }

    public class ConditionCheck
    {
        public string Type;
        public int? TargetId;
        public string TextValue;
        public string VariableName;
        public string VariableOperator;
        public int? CountryId;
        public bool IsNegatedGroup;
        public List<ConditionCheck> NestedConditions;
        public ConditionOperator NotOperator;
        public int NotValue;
    }

    public enum ConditionOperator
    {
        Exact,
        GreaterOrEqual,
        LessOrEqual,
        All
    }

    public class ThenBranch
    {
        public ConditionOperator Op;
        public int Value;
        public List<ScheduleNode> Children;
    }

    public class ElseBranch
    {
        public ConditionOperator Op;
        public int Value;
        public List<ScheduleNode> Children;
    }

    public class ConditionBlockNode : ScheduleNode
    {
        public int CountryId;
        public List<ConditionCheck> Conditions = new List<ConditionCheck>();
        public List<ThenBranch> ThenBranches = new List<ThenBranch>();
        public List<ElseBranch> ElseBranches = new List<ElseBranch>();
    }

    public class EveryoneNode : ScheduleNode
    {
        public List<ScheduleNode> Children;
    }

    public enum VariableOperation
    {
        Set,
        Add,
        Subtract
    }

    public class VariableNode : ScheduleNode
    {
        public string Name;
        public VariableOperation Operation;
        public string Value;
    }
}