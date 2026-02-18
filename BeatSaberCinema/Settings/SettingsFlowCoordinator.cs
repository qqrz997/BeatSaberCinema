using System;
using BeatSaberMarkupLanguage;
using HMUI;
using Zenject;

namespace BeatSaberCinema;

public class SettingsFlowCoordinator : FlowCoordinator
{
	[Inject] private readonly SettingsController settingsViewController = null!;

	public event Action? DidFinish;

	protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
	{
		try
		{
			if (firstActivation)
			{
				showBackButton = true;
			}

			if (addedToHierarchy)
			{
				ProvideInitialViewControllers(settingsViewController);
				SetTitle("Cinema Settings");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex);
		}
	}

	protected override void BackButtonWasPressed(ViewController viewController)
	{
		DidFinish?.Invoke();
	}
}