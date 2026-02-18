using Zenject;

namespace BeatSaberCinema.Menu;

public class BetterSongListFilterManager : IInitializable
{
	private bool filterAdded;

	public void Initialize()
	{
		if (InstalledMods.BetterSongList && !filterAdded)
		{
			AddFilter();
		}
	}

	private void AddFilter()
	{
		filterAdded = BetterSongList.FilterMethods.Register(new HasVideoFilter());

		if (filterAdded)
		{
			Plugin.Log.Debug($"Registered BetterSongList filter {nameof(HasVideoFilter)}");
		}
		else
		{
			Plugin.Log.Error($"Failed to register {nameof(HasVideoFilter)}");
		}
	}
}