using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AmongChess.Game
{
	public class End
	{
		public static int WinnerId = -1;

		[HarmonyPatch(typeof(EndGameManager))]
		public static class EndGameManagerPatch
		{
			[HarmonyPatch(nameof(EndGameManager.Start))]
			[HarmonyPrefix]
			public static void StartPatch(EndGameManager __instance)
			{
				int[] colorIds = (int[])Game.ColorIds.GetValue(Game.RealPlayerCount - 1);
				TextMeshPro winText = Object.Instantiate(__instance.WinText);
				__instance.WinText.enabled = false;
				EndGameResult.CachedWinners.Clear();
				if (WinnerId == -1)
				{
					winText.text = "Draw";
					winText.color = new Color32(127, 127, 127, 255);
				}
				else
				{
					int index = Utils.FindIndexById((byte)WinnerId);
					winText.text = Game.ColorNames[colorIds[index]] + " won";
					winText.color = Palette.PlayerColors[colorIds[index]];
					EndGameResult.CachedWinners.Add(new CachedPlayerData(Game.AllPlayers[index].Data));
				}
				Game.AllPlayers.Clear();
			}
		}

		[HarmonyPatch(typeof(GameManager))]
		public static class ShipStatusPatch
		{
			[HarmonyPatch(nameof(GameManager.RpcEndGame))]
			[HarmonyPrefix]
			public static bool RpcEndGamePatch(GameOverReason endReason)
			{
				return endReason != GameOverReason.CrewmatesByVote;
			}
		}
	}
}
