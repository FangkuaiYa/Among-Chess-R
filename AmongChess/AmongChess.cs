using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using Reactor;
using Reactor.Networking.Attributes;
using Reactor.Patches;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;
using UnityEngine;

namespace AmongChess
{
	[BepInPlugin("kylesmith0905.amongchess", "AmongChess", Version)]
	[BepInProcess("Among Us.exe")]
	[BepInDependency(ReactorPlugin.Id)]
	[ReactorModFlags(Reactor.Networking.ModFlags.RequireOnAllClients)]
	public class AmongChess : BasePlugin
	{
		public const string Version = "v1.2.2";

		public Harmony Harmony = new Harmony("kylesmith0905.amongchess");

		public override void Load()
		{
			ReactorCredits.Register("Among Chess", Version, false, ReactorCredits.AlwaysShow);

			ReactorVersionShower.TextUpdated += (text) =>
			{
				text.faceColor = new Color32(255, 165, 0, 255);
				text.fontSize = 3.2f;
				text.text = "Among Chess " + Version;
			};
			Harmony.PatchAll();
		}

		[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Start))]
		public static class CreateGameOptionsShowPatch
		{
			public static void Prefix(CreateGameOptions __instance)
			{
				__instance.tooltip.SetText("Create an Among Chess online lobby using the following settings.");

				Transform generalTab = GameObject.Find("GeneralTab").transform;

				// Remove the first four (map / settings picker) widgets
				for (int i = 0; i < 4; i++)
				{
					generalTab.GetChild(i).gameObject.Destroy();
				}

				// Shift the remaining widgets up to fill the removed space
				for (int i = 4; i < 8; i++)
				{
					Transform child = generalTab.GetChild(i);
					Vector3 pos = child.localPosition;
					child.localPosition = new Vector3(pos.x, pos.y + 1.7f, pos.z);
				}
			}
		}
	}
}