using System;
using HarmonyLib;
using JetBrains.Annotations;
using SiraUtil.Affinity;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace BeatSaberCinema.Patches;

public class SongPreviewPatch : IAffinity
{
	private readonly SongPreviewPlayerController songPreviewPlayerController;

	public SongPreviewPatch(SongPreviewPlayerController songPreviewPlayerController)
	{
		this.songPreviewPlayerController = songPreviewPlayerController;
	}

	[AffinityPostfix]
	[AffinityPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeTo),
		argumentTypes: [typeof(AudioClip), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Action)])]
	public void Postfix(SongPreviewPlayer __instance, AudioClip audioClip, float startTime, bool isDefault)
	{
		try
		{
			songPreviewPlayerController.SetFields(
				__instance._audioSourceControllers,
				__instance._channelsCount,
				__instance._activeChannel,
				audioClip,
				startTime,
				__instance._timeToDefaultAudioTransition,
				isDefault);
		}
		catch (Exception e)
		{
			Plugin.Log.Error(e);
		}
	}
}