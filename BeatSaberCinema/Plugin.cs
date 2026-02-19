using System.IO;
using System.Reflection;
using BeatSaberCinema.Installers;
using IPA;
using IPA.Config.Stores;
using IPA.Logging;
using IPA.Utilities;
using JetBrains.Annotations;
using SiraUtil.Zenject;
using SongCore;
using Zenject;
using Config = IPA.Config.Config;

namespace BeatSaberCinema;

[Plugin(RuntimeOptions.DynamicInit)]
[UsedImplicitly]
internal class Plugin
{
	public static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
	public static Logger Log { get; private set; } = null!; // Set in [Init]

	internal const string CAPABILITY = "Cinema";
	private static bool _enabled;

	internal static DiContainer menuContainer = null!;
	internal static DiContainer gameCoreContainer = null!;

	public static bool Enabled
	{
		get => _enabled && SettingsStore.Instance.PluginEnabled;
		private set => _enabled = value;
	}

	[Init]
	public Plugin(Logger logger, Config config, Zenjector zenjector)
	{
		Log = logger;
		zenjector.UseLogger(logger);
		SettingsStore.Instance = config.Generated<SettingsStore>();
		zenjector.Install<AppInstaller>(Location.App, SettingsStore.Instance);
		zenjector.Install<MenuInstaller>(Location.Menu);
		zenjector.Install<PlayerInstaller>(Location.Player);
	}

	[OnStart]
	public void OnApplicationStart()
	{
	}

	private static void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransition)
	{
		PlaybackController.Create();
	}

	[OnEnable]
	public void OnEnable()
	{
		Enabled = true;
		Collections.RegisterCapability(CAPABILITY);
		if (File.Exists(Path.Combine(UnityGame.InstallPath, "dxgi.dll")))
		{
			Log.Warn("dxgi.dll is present, video may fail to play. To fix this, delete the file dxgi.dll from your main Beat Saber folder (not in Plugins).");
		}
	}

	[OnDisable]
	[UsedImplicitly]
	public void OnDisable()
	{
		Enabled = false;

		//TODO Destroying and re-creating the PlaybackController messes up the VideoMenu without any exceptions in the Plugin.Log. Investigate.
		//PlaybackController.Destroy();

		StaticSingletons.VideoLoader.StopFileSystemWatcher();
		Collections.DeregisterCapability(CAPABILITY);
	}
}