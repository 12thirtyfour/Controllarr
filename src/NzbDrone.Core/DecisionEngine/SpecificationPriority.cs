namespace NzbDrone.Core.DecisionEngine
{
    public enum SpecificationPriority
    {
        Parsing = 0,
        Default = 1,
        Database = 2,
        Disk = 3
    }
}
