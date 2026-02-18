using System.IO;
using System.Reflection;
using BeatSaberCinema.Installers;
using BS_Utils.Utilities;
using IPA;
using IPA.Config.Stores;
using IPA.Logging;
using IPA.Utilities;
using IPA.Utilities.Async;
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
	private static bool _filterAdded;

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
	[UsedImplicitly]
	public void OnApplicationStart()
	{
		BSEvents.OnLoad();
	}

	private static void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransition)
	{
		PlaybackController.Create();

		AddBetterSongListFilter();
	}

	[OnEnable]
	[UsedImplicitly]
	public void OnEnable()
	{
		Enabled = true;
		BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;
		EnvironmentController.Init();
		Collections.RegisterCapability(CAPABILITY);
		if (File.Exists(Path.Combine(UnityGame.InstallPath, "dxgi.dll")))
		{
			Log.Warn("dxgi.dll is present, video may fail to play. To fix this, delete the file dxgi.dll from your main Beat Saber folder (not in Plugins).");
		}

		//No need to index maps if the filter isn't going to be applied anyway

	}

	[OnDisable]
	[UsedImplicitly]
	public void OnDisable()
	{
		Enabled = false;
		BSEvents.lateMenuSceneLoadedFresh -= OnMenuSceneLoadedFresh;

		//TODO Destroying and re-creating the PlaybackController messes up the VideoMenu without any exceptions in the Plugin.Log. Investigate.
		//PlaybackController.Destroy();

		EnvironmentController.Disable();
		StaticSingletons.VideoLoader.StopFileSystemWatcher();
		Collections.DeregisterCapability(CAPABILITY);
	}

	private static void AddBetterSongListFilter()
	{
		if (!InstalledMods.BetterSongList || _filterAdded)
		{
			return;
		}

		_filterAdded = BetterSongList.FilterMethods.Register(new HasVideoFilter());

		if (_filterAdded)
		{
			Log.Debug($"Registered {nameof(HasVideoFilter)}");
		}
		else
		{
			Log.Error($"Failed to register {nameof(HasVideoFilter)}");
		}
	}
}