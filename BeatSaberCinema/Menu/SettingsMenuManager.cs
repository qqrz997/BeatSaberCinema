using System;
using BeatSaberMarkupLanguage.MenuButtons;
using Zenject;

namespace BeatSaberCinema.Menu;

internal class SettingsMenuManager : IInitializable, IDisposable
{
	private readonly MenuButtons menuButtons;
	private readonly MainFlowCoordinator mainFlowCoordinator;
	private readonly SettingsFlowCoordinator settingsFlowCoordinator;

	private readonly MenuButton button;

	public SettingsMenuManager(
		MenuButtons menuButtons,
		MainFlowCoordinator mainFlowCoordinator,
		SettingsFlowCoordinator settingsFlowCoordinator)
	{
		this.menuButtons = menuButtons;
		this.mainFlowCoordinator = mainFlowCoordinator;
		this.settingsFlowCoordinator = settingsFlowCoordinator;

		button = new("Cinema", "Cinema Settings", ShowFlowCoordinator);
	}

	public void Initialize()
	{
		settingsFlowCoordinator.DidFinish += DismissFlowCoordinator;
		menuButtons.RegisterButton(button);
	}

	public void Dispose()
	{
		settingsFlowCoordinator.DidFinish -= DismissFlowCoordinator;
		menuButtons.UnregisterButton(button);
	}

	private void ShowFlowCoordinator()
	{
		mainFlowCoordinator.PresentFlowCoordinator(settingsFlowCoordinator);
	}

	private void DismissFlowCoordinator()
	{
		mainFlowCoordinator.DismissFlowCoordinator(settingsFlowCoordinator);
	}
}