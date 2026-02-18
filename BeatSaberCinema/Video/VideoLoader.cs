using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeatmapEditor3D.DataModels;
using IPA.Utilities.Async;
using Newtonsoft.Json;
using SiraUtil.Zenject;
using SongCore;
using UnityEngine;
using Zenject;

namespace BeatSaberCinema;

internal class VideoLoader : IInitializable, IAsyncInitializable, IDisposable
{
	public const string WIP_DIRECTORY_NAME = "CinemaWIPVideos";
	public const string WIP_MAPS_FOLDER = "CustomWIPLevels";

	private const string OST_DIRECTORY_NAME = "CinemaOSTVideos";
	private const string CONFIG_FILENAME = "cinema-video.json";
	private const string CONFIG_FILENAME_MVP = "video.json";

	private  FileSystemWatcher? _fileSystemWatcher;
	public  event Action<VideoConfig?>? ConfigChanged;
	private  string? _ignoreNextEventForPath;

	//This should ideally be a HashSet, but there is no concurrent version of it. We also don't need the value, so use the smallest possible type.
	public readonly ConcurrentDictionary<string, byte> MapsWithVideo = new();
	private readonly ConcurrentDictionary<string, VideoConfig> CachedConfigs = new();
	private readonly ConcurrentDictionary<string, VideoConfig> BundledConfigs = new();

	private  BeatmapLevelsModel? _beatmapLevelsModel;
	private  BeatmapLevelsEntitlementModel? _beatmapLevelsEntitlementModel;
	private  AudioClipAsyncLoader? _audioClipAsyncLoader;
	private  CustomLevelLoader? _customLevelLoader;

	private  BeatmapLevelsModel BeatmapLevelsModel =>
		_beatmapLevelsModel ??= Plugin.menuContainer.Resolve<BeatmapLevelsModel>();
	private  BeatmapLevelsEntitlementModel BeatmapLevelsEntitlementModel =>
		_beatmapLevelsEntitlementModel ??= BeatmapLevelsModel._entitlements;
	private  AudioClipAsyncLoader AudioClipAsyncLoader =>
		_audioClipAsyncLoader ??= Plugin.menuContainer.Resolve<AudioClipAsyncLoader>();
	public  CustomLevelLoader CustomLevelLoader
	{
		get
		{
			if (_customLevelLoader == null)
			{
				_customLevelLoader = Resources.FindObjectsOfTypeAll<CustomLevelLoader>().First();
			}
			return _customLevelLoader;
		}
	}

	public async Task InitializeAsync(CancellationToken token)
	{
		foreach (var config in await LoadBundledConfigs())
		{
			BundledConfigs.TryAdd(config.levelID, config.config);
		}
	}

	public void Initialize()
	{
		if (InstalledMods.BetterSongList)
		{
			Loader.SongsLoadedEvent += IndexMaps;
		}
	}

	public void Dispose()
	{
		Loader.SongsLoadedEvent -= IndexMaps;
	}

	public async void IndexMaps(Loader loader, ConcurrentDictionary<string, BeatmapLevel> beatmapLevels)
	{
		Plugin.Log.Debug("Indexing maps...");
		var stopwatch = new Stopwatch();
		stopwatch.Start();

		var officialMaps = GetOfficialMaps();

		void Action()
		{
			var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, (Environment.ProcessorCount / 2) - 1) };
			Parallel.ForEach(Loader.CustomLevels, options, IndexMap);
			if (officialMaps.Count > 0)
			{
				Parallel.ForEach(officialMaps, options, IndexMap);
			}
		}

		var loadingTask = new Task((Action) Action, CancellationToken.None);
		var loadingAwaiter = loadingTask.ConfigureAwait(false);
		loadingTask.Start();
		await loadingAwaiter;

		Plugin.Log.Debug($"Indexing took {stopwatch.ElapsedMilliseconds} ms");
	}

	private  List<BeatmapLevel> GetOfficialMaps()
	{
		var officialMaps = new List<BeatmapLevel>();

		AddOfficialPackCollection(BeatmapLevelsModel.ostAndExtrasBeatmapLevelsRepository);
		AddOfficialPackCollection(BeatmapLevelsModel.dlcBeatmapLevelsRepository);

		return officialMaps;

		void AddOfficialPackCollection(BeatmapLevelsRepository beatmapLevelsRepository)
		{
			officialMaps.AddRange(beatmapLevelsRepository.beatmapLevelPacks.SelectMany(pack => pack._beatmapLevels));
		}
	}

	private  void IndexMap(KeyValuePair<string, BeatmapLevel> levelKeyValuePair)
	{
		IndexMap(levelKeyValuePair.Value);
	}

	private  void IndexMap(BeatmapLevel level)
	{
		var configPath = GetConfigPath(level);
		if (File.Exists(configPath))
		{
			MapsWithVideo.TryAdd(level.levelID, 0);
		}
	}

	private  string GetConfigPath(BeatmapLevel level)
	{
		var levelPath = GetLevelPath(level);
		return Path.Combine(levelPath, CONFIG_FILENAME);
	}

	public  string GetConfigPath(string levelPath)
	{
		return Path.Combine(levelPath, CONFIG_FILENAME);
	}

	public  void AddConfigToCache(VideoConfig config, BeatmapLevel level)
	{
		var success = CachedConfigs.TryAdd(level.levelID, config);
		MapsWithVideo.TryAdd(level.levelID, 0);
		if (success)
		{
			Plugin.Log.Debug($"Adding config for {level.levelID} to cache");
		}
	}

	public  void RemoveConfigFromCache(BeatmapLevel level)
	{
		var success = CachedConfigs.TryRemove(level.levelID, out _);
		if (success)
		{
			Plugin.Log.Debug($"Removing config for {level.levelID} from cache");
		}
	}

	private  VideoConfig? GetConfigFromCache(BeatmapLevel level)
	{
		var success = CachedConfigs.TryGetValue(level.levelID, out var config);
		if (success)
		{
			Plugin.Log.Debug($"Loading config for {level.levelID} from cache");
		}
		return config;
	}

	private  VideoConfig? GetConfigFromBundledConfigs(BeatmapLevel level)
	{
		var levelID = !level.hasPrecalculatedData ? level.levelID : Util.ReplaceIllegalFilesystemChars(level.songName.Trim());
		BundledConfigs.TryGetValue(levelID, out var config);

		if (config == null)
		{
			Plugin.Log.Debug($"No bundled config found for {levelID}");
			return null;
		}

		config.LevelDir = GetLevelPath(level);
		config.bundledConfig = true;
		Plugin.Log.Debug("Loaded from bundled configs");
		return config;
	}

	public  void StopFileSystemWatcher()
	{
		Plugin.Log.Debug("Disposing FileSystemWatcher");
		_fileSystemWatcher?.Dispose();
	}

	public  void SetupFileSystemWatcher(BeatmapLevel level)
	{
		var levelPath = GetLevelPath(level);
		ListenForConfigChanges(levelPath);
	}

	public  void SetupFileSystemWatcher(string path)
	{
		ListenForConfigChanges(path);
	}

	private  void ListenForConfigChanges(string levelPath)
	{
		_fileSystemWatcher?.Dispose();
		if (!Directory.Exists(levelPath))
		{
			if (File.Exists(levelPath))
			{
				levelPath = Path.GetDirectoryName(levelPath)!;
			}
			else
			{
				Plugin.Log.Debug($"Level directory {levelPath} does not exist");
				return;
			}
		}

		Plugin.Log.Debug($"Setting up FileSystemWatcher for {levelPath}");

		_fileSystemWatcher = new();
		var configPath = GetConfigPath(levelPath);
		_fileSystemWatcher.Path = Path.GetDirectoryName(configPath);
		_fileSystemWatcher.Filter = Path.GetFileName(configPath);
		_fileSystemWatcher.EnableRaisingEvents = true;

		_fileSystemWatcher.Changed += OnConfigChanged;
		_fileSystemWatcher.Created += OnConfigChanged;
		_fileSystemWatcher.Deleted += OnConfigChanged;
		_fileSystemWatcher.Renamed += OnConfigChanged;
	}

	private  void OnConfigChanged(object _, FileSystemEventArgs e)
	{
		UnityMainThreadTaskScheduler.Factory.StartNew(delegate { OnConfigChangedMainThread(e); });
	}

	private  void OnConfigChangedMainThread(FileSystemEventArgs e)
	{
		Plugin.Log.Debug("Config "+e.ChangeType+" detected: "+e.FullPath);
		if (_ignoreNextEventForPath == e.FullPath && !Util.IsInEditor())
		{
			Plugin.Log.Debug("Ignoring event after saving");
			_ignoreNextEventForPath = null;
			return;
		}
		CoroutineStarter.Instance.StartCoroutine(WaitForConfigWriteCoroutine(e));
	}

	private  IEnumerator WaitForConfigWriteCoroutine(FileSystemEventArgs e)
	{
		if (e.ChangeType == WatcherChangeTypes.Deleted)
		{
			ConfigChanged?.Invoke(null);
			yield break;
		}

		var configPath = e.FullPath;
		var configFileInfo = new FileInfo(configPath);
		var timeout = new Timeout(3f);
		yield return new WaitUntil(() =>
			!Util.IsFileLocked(configFileInfo) || timeout.HasTimedOut);
		var config = LoadConfig(configPath);
		ConfigChanged?.Invoke(config);
	}

	public  bool IsDlcSong(BeatmapLevel level)
	{
		return level.GetType() == typeof(BeatmapLevelSO);
	}

	public  async Task<AudioClip?> GetAudioClipForLevel(BeatmapLevel level)
	{
		if (!IsDlcSong(level))
		{
			return await LoadAudioClipAsync(level);
		}

		var beatmapLevelLoader = (BeatmapLevelLoader)BeatmapLevelsModel.levelLoader;
		if (beatmapLevelLoader._loadedBeatmapLevelDataCache.TryGetFromCache(level.levelID, out var beatmapLevelData))
		{
			Plugin.Log.Debug("Getting audio clip from async cache");
			return await _audioClipAsyncLoader.LoadSong(beatmapLevelData);
		}

		return await LoadAudioClipAsync(level);
	}

	private  async Task<AudioClip?> LoadAudioClipAsync(BeatmapLevel level)
	{
		var loaderTask = AudioClipAsyncLoader.LoadPreview(level);
		if (loaderTask == null)
		{
			Plugin.Log.Error("AudioClipAsyncLoader.LoadPreview() failed");
			return null;
		}

		return await loaderTask;
	}

	public  async Task<EntitlementStatus> GetEntitlementForLevel(BeatmapLevel level)
	{
		return await BeatmapLevelsEntitlementModel.GetLevelEntitlementStatusAsync(level.levelID, CancellationToken.None);
	}

	public  VideoConfig? GetConfigForEditorLevel(BeatmapDataModel _, string originalPath)
	{
		if (!Directory.Exists(originalPath))
		{
			Plugin.Log.Debug($"Path does not exist: {originalPath}");
			return null;
		}

		var configPath = GetConfigPath(originalPath);
		var videoConfig = LoadConfig(configPath);

		return videoConfig;
	}

	public  VideoConfig? GetConfigForLevel(BeatmapLevel? level)
	{
		if (InstalledMods.BeatSaberPlaylistsLib)
		{
			level = level.GetLevelFromPlaylistIfAvailable();
		}

		if (level == null)
		{
			return null;
		}

		var cachedConfig = GetConfigFromCache(level);
		if (cachedConfig != null)
		{
			if (cachedConfig.DownloadState == DownloadState.Downloaded)
			{
				RemoveConfigFromCache(level);
			}
			return cachedConfig;
		}

		VideoConfig? videoConfig = null;
		var levelPath = GetLevelPath(level);
		if (Directory.Exists(levelPath))
		{
			videoConfig = LoadConfig(GetConfigPath(levelPath));
			if (videoConfig == null && !InstalledMods.MusicVideoPlayer)
			{
				//Back compatiblity with MVP configs, but only if MVP is not installed
				videoConfig = LoadConfig(Path.Combine(levelPath, CONFIG_FILENAME_MVP));
			}
		}
		else
		{
			Plugin.Log.Debug($"Path does not exist: {levelPath}");
		}

		if (InstalledMods.BeatSaberPlaylistsLib && videoConfig == null && level.TryGetPlaylistLevelConfig(levelPath, out var playlistConfig))
		{
			videoConfig = playlistConfig;
		}

		return videoConfig ?? GetConfigFromBundledConfigs(level);
	}
	public  string GetLevelPath(BeatmapLevel level)
	{
		if (!level.hasPrecalculatedData
		    && CustomLevelLoader._loadedBeatmapSaveData.TryGetValue(level.levelID, out var levelData))
		{
			return levelData.customLevelFolderInfo.folderPath;
		}

		var songName = level.songName.Trim();
		songName = Util.ReplaceIllegalFilesystemChars(songName);
		return Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", OST_DIRECTORY_NAME, songName);
	}

	public  void SaveVideoConfig(VideoConfig videoConfig)
	{
		if (videoConfig.LevelDir == null || videoConfig.ConfigPath == null || !Directory.Exists(videoConfig.LevelDir))
		{
			Plugin.Log.Warn("Failed to save video. Path "+videoConfig.LevelDir+" does not exist.");
			return;
		}

		if (videoConfig.IsWIPLevel)
		{
			videoConfig.configByMapper = true;
		}

		var configPath = videoConfig.ConfigPath;
		SaveVideoConfigToPath(videoConfig, configPath);
	}

	private  void SaveVideoConfigToPath(VideoConfig config, string configPath)
	{
		_ignoreNextEventForPath = configPath;
		Plugin.Log.Info($"Saving video config to {configPath}");

		try
		{
			File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
			config.NeedsToSave = false;
		}
		catch (Exception e)
		{
			Plugin.Log.Error("Failed to save level data: ");
			Plugin.Log.Error(e);
		}

		if (!File.Exists(configPath))
		{
			Plugin.Log.Error("Config file doesn't exist after saving!");
		}
		else
		{
			Plugin.Log.Debug("Config save successful");
		}
	}

	public  void DeleteVideo(VideoConfig videoConfig)
	{
		if (videoConfig.VideoPath == null)
		{
			Plugin.Log.Warn("Tried to delete video, but its path was null");
			return;
		}

		try
		{
			File.Delete(videoConfig.VideoPath);
			Plugin.Log.Info("Deleted video at "+videoConfig.VideoPath);
			if (videoConfig.DownloadState != DownloadState.Cancelled)
			{
				videoConfig.DownloadState = DownloadState.NotDownloaded;
			}

			videoConfig.videoFile = null;
		}
		catch (Exception e)
		{
			Plugin.Log.Error("Failed to delete video at "+videoConfig.VideoPath);
			Plugin.Log.Error(e);
		}
	}

	public  bool DeleteConfig(VideoConfig videoConfig, BeatmapLevel level)
	{
		if (videoConfig.LevelDir == null)
		{
			Plugin.Log.Error("LevelDir was null when trying to delete config");
			return false;
		}

		try
		{
			var cinemaConfigPath = GetConfigPath(videoConfig.LevelDir);
			if (File.Exists(cinemaConfigPath))
			{
				File.Delete(cinemaConfigPath);
			}

			var mvpConfigPath = Path.Combine(videoConfig.LevelDir, CONFIG_FILENAME_MVP);
			if (File.Exists(mvpConfigPath))
			{
				File.Delete(mvpConfigPath);
			}

			MapsWithVideo.TryRemove(level.levelID, out _);
		}
		catch (Exception e)
		{
			Plugin.Log.Error("Failed to delete video config:");
			Plugin.Log.Error(e);
		}

		RemoveConfigFromCache(level);
		Plugin.Log.Info("Deleted video config");

		return true;
	}

	private  VideoConfig? LoadConfig(string configPath)
	{
		if (!File.Exists(configPath))
		{
			return null;
		}

		VideoConfig? videoConfig;
		try
		{
			var json = File.ReadAllText(configPath);
			if (configPath.EndsWith("\\" + CONFIG_FILENAME_MVP))
			{
				//Back compatiblity with MVP configs
				var videoConfigListBackCompat = JsonConvert.DeserializeObject<VideoConfigListBackCompat>(json);
				if (videoConfigListBackCompat == null)
				{
					Plugin.Log.Warn($"Deserializing video config at {configPath} failed");
					return null;
				}
				videoConfig = new(videoConfigListBackCompat);
			}
			else
			{
				videoConfig = JsonConvert.DeserializeObject<VideoConfig>(json);
			}
		}
		catch (Exception e)
		{
			Plugin.Log.Error($"Error parsing video json {configPath}:");
			Plugin.Log.Error(e);
			return null;
		}

		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (videoConfig != null)
		{
			videoConfig.LevelDir = Path.GetDirectoryName(configPath);
			videoConfig.UpdateDownloadState();
		}
		else
		{
			Plugin.Log.Warn($"Deserializing video config at {configPath} failed");
		}

		return videoConfig;
	}

	private  async Task<IEnumerable<BundledConfig>> LoadBundledConfigs()
	{
		var buffer = await BeatSaberMarkupLanguage.Utilities.GetResourceAsync(Plugin.Assembly, "BeatSaberCinema.Resources.configs.json");
		var jsonString = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
		var configs = JsonConvert.DeserializeObject<BundledConfig[]>(jsonString);
		if (configs == null)
		{
			Plugin.Log.Error("Failed to deserialize bundled configs");
			return Enumerable.Empty<BundledConfig>();
		}
		return configs;
	}
}

[Serializable]
internal class BundledConfig
{
	public string levelID = null!;
	public VideoConfig config = null!;
}