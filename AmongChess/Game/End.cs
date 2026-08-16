using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AmongChess.Game
{
	public class End
	{
		public static int WinnerId = -1;
		internal static Chess.EnumResults Result = Chess.EnumResults.MoveNormal;

		[HarmonyPatch(typeof(EndGameManager))]
		public static class EndGameManagerPatch
		{
			[HarmonyPatch(nameof(EndGameManager.Start))]
			[HarmonyPrefix]
			public static void StartPatch(EndGameManager __instance)
			{
				try
				{
					EndGameResult.CachedWinners.Clear();
					if (WinnerId != -1)
					{
						int index = Utils.FindIndexById(WinnerId);
						if (index >= 0 && index < Game.AllPlayers.Count && Game.AllPlayers[index] != null && Game.AllPlayers[index].Data != null)
						{
							EndGameResult.CachedWinners.Add(new CachedPlayerData(Game.AllPlayers[index].Data)
							{
								IsYou = Game.AllPlayers[index].PlayerId == PlayerControl.LocalPlayer.PlayerId
							});
						}
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] End StartPatch failed: " + e);
				}
			}

			[HarmonyPatch(nameof(EndGameManager.SetEverythingUp))]
			[HarmonyPostfix]
			public static void SetEverythingUpPatch(EndGameManager __instance)
			{
				try
				{
					bool isDraw = WinnerId == -1;
					int winnerIndex = isDraw ? -1 : Utils.FindIndexById(WinnerId);
					bool isWhite = winnerIndex == 0;
					// Use the winner's real in-game color for the background bar so a black-side win
					// is a bright bar (black side actually uses White player color id 6), never pure black.
					Color playerColor = Color.white;
					Color bgColor = new Color(0.4f, 0.4f, 0.4f, 1f);
					if (!isDraw)
					{
						int[] colorIds = (int[])Game.ColorIds.GetValue(Game.RealPlayerCount > 0 ? Game.RealPlayerCount - 1 : 0);
						int colorId = (winnerIndex >= 0 && winnerIndex < colorIds.Length) ? colorIds[winnerIndex] : 0;
						if (colorId >= 0 && colorId < Palette.PlayerColors.Length) playerColor = Palette.PlayerColors[colorId];
						bgColor = playerColor;
					}
					// Title always readable on the dark end screen: winner color if bright, else white.
					Color titleColor = isDraw ? Color.white : (playerColor.grayscale > 0.5f ? playerColor : Color.white);
					string titleText = isDraw ? "Draw" : (isWhite ? "White wins" : "Black wins");
					if (__instance.WinText != null)
					{
						__instance.WinText.text = titleText;
						__instance.WinText.color = titleColor;
					}
					if (__instance.BackgroundBar != null)
					{
						__instance.BackgroundBar.material.SetColor("_Color", bgColor);
					}
					// Reason subtitle under the title.
					string reason = ReasonText(Result);
					if (reason.Length > 0 && __instance.WinText != null)
					{
						TextMeshPro subtitle = Object.Instantiate(__instance.WinText.gameObject).GetComponent<TextMeshPro>();
						subtitle.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.7f, __instance.WinText.transform.position.z);
						subtitle.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
						subtitle.text = reason;
						subtitle.color = titleColor;
					}
					// Winner's name under the subtitle so the winning player is always identified
					// (the vanilla end screen only names avatars on a victory for the local player).
					if (!isDraw && winnerIndex >= 0 && winnerIndex < Game.AllPlayers.Count && Game.AllPlayers[winnerIndex] != null && Game.AllPlayers[winnerIndex].Data != null && __instance.WinText != null)
					{
						TextMeshPro winnerName = Object.Instantiate(__instance.WinText.gameObject).GetComponent<TextMeshPro>();
						winnerName.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 1.4f, __instance.WinText.transform.position.z);
						winnerName.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
						winnerName.text = Game.AllPlayers[winnerIndex].Data.PlayerName;
						winnerName.color = titleColor;
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] End SetEverythingUp failed: " + e);
				}
				Game.AllPlayers.Clear();
			}

			internal static string ReasonText(Chess.EnumResults results)
			{
				switch (results)
				{
					case Chess.EnumResults.WinCheckmate: return "by checkmate";
					case Chess.EnumResults.WinTimeout: return "by timeout";
					case Chess.EnumResults.WinResignation: return "by resignation";
					case Chess.EnumResults.DrawStalemate: return "draw by stalemate";
					case Chess.EnumResults.DrawMaterial: return "draw by insufficient material";
					case Chess.EnumResults.DrawFifty: return "draw by fifty-move rule";
					case Chess.EnumResults.DrawRepetition: return "draw by repetition";
					case Chess.EnumResults.DrawAgreement: return "draw by agreement";
					case Chess.EnumResults.DrawTimeout: return "draw by timeout";
					default: return "";
				}
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
