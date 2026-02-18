using BeatSaberCinema.Menu;
using BeatSaberCinema.Patches;
using Zenject;

namespace BeatSaberCinema.Installers;

internal class MenuInstaller : Installer
{
	public override void InstallBindings()
	{
		Container.BindInterfacesAndSelfTo<VideoMenu>().AsSingle();
		Container.Bind<SettingsController>().FromNewComponentAsViewController().AsSingle();
		Container.Bind<SettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();

		Container.BindInterfacesTo<GameplaySetupTabManager>().AsSingle();
		Container.BindInterfacesTo<SettingsMenuManager>().AsSingle();

		Container.BindInterfacesAndSelfTo<SongPreviewPlayerController>().AsSingle();

		// Affinity patches
		Container.BindInterfacesTo<SongPreviewPatch>().AsSingle();
	}
}