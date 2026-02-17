using Zenject;

namespace BeatSaberCinema.Installers;

internal class AppInstaller : Installer
{
	private readonly SettingsStore config;

	public AppInstaller(SettingsStore config)
	{
		this.config = config;
	}

	public override void InstallBindings()
	{
		Container.BindInstance(config).AsSingle();
	}
}