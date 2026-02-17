using BeatmapEditor3D.DataModels;

namespace BeatSaberCinema;

public class LevelSelectedArgs
{
	public readonly BeatmapLevel? BeatmapLevel;
	public readonly BeatmapDataModel? BeatmapData;
	public readonly string? OriginalPath;

	public LevelSelectedArgs(BeatmapLevel? level, BeatmapDataModel? beatmapData = null, string? originalPath = null)
	{
		BeatmapLevel = level;
		BeatmapData = beatmapData;
		OriginalPath = originalPath;
	}
}

public class SongDataArgs
{
	public readonly SongCore.Data.SongData? SongData;
	public readonly SongCore.Data.SongData.DifficultyData? SelectedDifficultyData;

	public SongDataArgs(SongCore.Data.SongData? songData, SongCore.Data.SongData.DifficultyData? selectedDifficultyData)
	{
		SongData = songData;
		SelectedDifficultyData = selectedDifficultyData;
	}
}