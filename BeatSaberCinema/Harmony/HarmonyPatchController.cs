using System;
using System.Linq;
using HarmonyLib;
using Zenject;

namespace BeatSaberCinema;

public class HarmonyPatchController : IInitializable, IDisposable
{
	private readonly Harmony _harmonyInstance = new("com.github.kevga.cinema");

	public void Initialize()
	{
		var patchClassProcessors = AccessTools.GetTypesFromAssembly(Plugin.Assembly)
			.Where(type => type.FullName is { } name && name.StartsWith("BeatSaberCinema.Patches"))
			.Select(type => _harmonyInstance.CreateClassProcessor(type));

		foreach (var patchClassProcessor in patchClassProcessors)
		{
			try
			{
				patchClassProcessor.Patch();
			}
			catch (Exception e)
			{
				Plugin.Log.Error(e);
			}
		}
	}

	public void Dispose()
	{
		_harmonyInstance.UnpatchSelf();
	}
}