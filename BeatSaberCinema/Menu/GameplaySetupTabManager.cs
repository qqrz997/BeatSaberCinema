using System;
using BeatSaberMarkupLanguage.GameplaySetup;
using Zenject;

namespace BeatSaberCinema.Menu;

internal class GameplaySetupTabManager : IInitializable, IDisposable
{
	private readonly GameplaySetup gameplaySetup;
	private readonly VideoMenu videoMenu;

	public GameplaySetupTabManager(GameplaySetup gameplaySetup, VideoMenu videoMenu)
	{
		this.gameplaySetup = gameplaySetup;
		this.videoMenu = videoMenu;
	}

	public void Initialize()
	{
		gameplaySetup.AddTab("Cinema", "BeatSaberCinema.VideoMenu.Views.video-menu.bsml", videoMenu);
	}

	public void Dispose()
	{
		gameplaySetup.RemoveTab("Cinema");
	}
}