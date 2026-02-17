namespace BeatSaberCinema;

internal class StaticSingletons
{
	public StaticSingletons(VideoLoader videoLoader)
	{
		VideoLoader = videoLoader;
	}

	public static VideoLoader VideoLoader { get; private set; } = null!;
}