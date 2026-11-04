using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Indexers;

public enum PlatformPackSeedGoal
{
    [FieldOption(Label = "IndexerSettingsPlatformPackSeedGoalUseStandardGoals")]
    UseStandardSeedGoal = 0,
    [FieldOption(Label = "IndexerSettingsPlatformPackSeedGoalUseSeasonPackGoals")]
    UsePlatformPackSeedGoal = 1
}
