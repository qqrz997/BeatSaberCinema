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
		Plugin.Log.Debug("Hardware info:\n"+Util.GetHardwareInfo());

		Container.BindInstance(config).AsSingle();

		// todo: temporary
		Container.Bind<StaticSingletons>().AsSingle().NonLazy();

		Container.BindInterfacesTo<HarmonyPatchController>().AsSingle();
		Container.BindInterfacesAndSelfTo<VideoLoader>().AsSingle();
	}
}