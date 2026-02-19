namespace BeatSaberCinema;

internal class StaticSingletons
{
	public StaticSingletons(VideoLoader videoLoader, EnvironmentController environmentController)
	{
		VideoLoader = videoLoader;
		EnvironmentController = environmentController;
	}

	public static VideoLoader VideoLoader { get; private set; } = null!;
	public static EnvironmentController EnvironmentController { get; private set; } = null!;
}