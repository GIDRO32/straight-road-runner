/// <summary>
/// Live spawn chances during a run. Built by StageControl from the stage's lists;
/// DifficultyManager nudges <see cref="chance"/> within <see cref="range"/>.
/// </summary>
public class PlatformSpawnState
{
    public PlatformDefinition definition;
    public DifficultyRange range;
    public float chance;
}

public class ObstacleSpawnState
{
    public ObstacleDefinition definition;
    public DifficultyRange range;
    public float chance;
}
