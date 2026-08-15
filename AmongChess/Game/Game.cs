using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AmongChess.Rpc;

namespace AmongChess.Game
{
	internal class Game
	{
		public static List<PlayerControl> AllPlayers = new List<PlayerControl> { };
		public static List<CustomPlayer> AllCustomPlayers = new List<CustomPlayer> { };
		public static int[][] ColorIds = new int[2][] { new int[1] { 1 }, new int[2] { 7, 6 } };
		public static string[] ColorNames = new string[18] { "Red", "Blue", "Green", "Pink", "Orange", "Yellow", "Black", "White", "Purple", "Brown", "Cyan", "Lime", "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral" };

		public static readonly Dictionary<PlayerControl, (byte x, byte y)> PieceCoords = new Dictionary<PlayerControl, (byte x, byte y)>();

		public static int RealPlayerCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
				{
					PlayerControl pc = PlayerControl.AllPlayerControls[i];
					if (pc != null && !pc.isDummy) count++;
				}
				return count;
			}
		}
		public static byte PlayerTurn = 0;
		public static uint TotalTurns = 0;
		public static EnumActivity LocalActivity
		{
			get
			{
				if (PlayerControl.LocalPlayer == null) return EnumActivity.Lobby;
				CustomPlayer cp = AllCustomPlayers.Count < 1 ? null : Utils.FindCustom(PlayerControl.LocalPlayer.PlayerId);
				return cp != null ? cp.Activity : EnumActivity.Lobby;
			}
			set
			{
				if (PlayerControl.LocalPlayer == null) return;
				CustomPlayer cp = Utils.FindCustom(PlayerControl.LocalPlayer.PlayerId);
				if (cp != null) cp.Activity = value;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
		public static class PlayerControlPatch
		{
			public static bool Prefix(PlayerControl __instance)
			{
				try
				{
					if (__instance.isDummy)
					{
						// Chess piece: snap it to its board square every fixed frame and skip vanilla
						// FixedUpdate entirely. PRIMARY source is the piece NAME, because PlayMove()
						// renames a piece to its NEW square when it moves (e.g. "P:3,1") — a coords map
						// would hold stale values and REVERT the move every frame. A captured piece is
						// renamed to "P:D" (no comma) → no snap → it stays in the graveyard.
						if (__instance.name != null && __instance.name.IndexOf(':') != -1)
						{
							int comma = __instance.name.IndexOf(',');
							if (comma != -1)
							{
								int colon = __instance.name.IndexOf(':');
								byte px = byte.Parse(__instance.name.Substring(colon + 1, comma - colon - 1));
								byte py = byte.Parse(__instance.name.Substring(comma + 1));
								__instance.transform.position = new Vector3((px * 0.5f) + 16, (py * -0.5f) - 10, __instance.transform.position.z);
							}
							// else: captured ("P:D") — leave at graveyard position.
						}
						else if (PieceCoords.TryGetValue(__instance, out (byte x, byte y) coords))
						{
							// Fallback only if the name has no colon at all (name fully overwritten).
							__instance.transform.position = new Vector3((coords.x * 0.5f) + 16, (coords.y * -0.5f) - 10, __instance.transform.position.z);
						}
						if (__instance.cosmetics != null)
						{
							// Hide name + any colorblind/color-name text.
							TextMeshPro[] texts = __instance.GetComponentsInChildren<TextMeshPro>(true);
							for (int i = 0; i < texts.Length; i++)
							{
								if (texts[i] != null)
									texts[i].color = new Color(1f, 1f, 1f, 0f);
							}
						}
						return false;
					}
					__instance.Visible = true;
				}
				catch (System.Exception) { }
				return true;
			}
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
		public static class PlayerPhysicsPatch
		{
			public static bool Prefix(PlayerPhysics __instance)
			{
				// Chess pieces are static board objects — skip vanilla physics for them.
				PlayerControl pc = __instance.gameObject.GetComponent<PlayerControl>();
				if (pc != null && pc.isDummy) return false;
				return true;
			}
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
		public static class OnPlayerLeftPatch
		{
			public static void Postfix()
			{
				try
				{
					if (PlayerControl.LocalPlayer == null) return;
					EventEnded(Chess.EnumResults.WinResignation, PlayerControl.LocalPlayer.PlayerId, true);
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] OnPlayerLeft patch failed: " + e);
				}
			}
		}

		[HarmonyPatch(typeof(HudManager), nameof(HudManager.ShowTaskComplete))]
		public static class ShowTaskCompletePatch
		{
			public static void Prefix(HudManager __instance)
			{
				try
				{
					// TaskCompleteOverlay is a public field and exists even while inactive.
					// GameObject.Find would return null because the overlay is only activated
					// by a coroutine AFTER ShowTaskComplete runs (→ NRE that broke ShipStatus init).
					if (__instance == null || __instance.TaskCompleteOverlay == null) return;
					TextMeshPro text = __instance.TaskCompleteOverlay.GetComponentInChildren<TextMeshPro>(true);
					if (text != null) text.text = "It's Your Turn";
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] ShowTaskComplete patch failed: " + e);
				}
			}
		}

		public static void EventEnded(Chess.EnumResults results, byte winnerId, bool rpcSend)
		{
			if (rpcSend == true)
			{
				MessageWriter rpcMessageLocal = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.GameResult, SendOption.Reliable, -1);
				rpcMessageLocal.Write((byte)results);
				rpcMessageLocal.Write(winnerId);
				AmongUsClient.Instance.FinishRpcImmediately(rpcMessageLocal);
			}
			LocalActivity = EnumActivity.GameEnd;
			PlayerControl winnerPlayer = Utils.FindPlayer(winnerId);
			if (winnerPlayer == null || winnerPlayer.Data == null) return;
			int colorId = winnerPlayer.Data.DefaultOutfit.ColorId;
			string colorName = ColorNames[colorId].ToString();
			if (results == Chess.EnumResults.DrawStalemate)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by stalemate.");
			}
			else if (results == Chess.EnumResults.DrawMaterial)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by insufficient material.");
			}
			else if (results == Chess.EnumResults.DrawFifty)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by the fifty-move-rule.");
			}
			else if (results == Chess.EnumResults.DrawRepetition)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by repetition.");
			}
			else if (results == Chess.EnumResults.DrawAgreement)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by agreement.");
			}
			else if (results == Chess.EnumResults.DrawTimeout)
			{
				HudManager.Instance.ShowPopUp("The game ended in a draw by timeout.");
			}
			else if (results == Chess.EnumResults.WinCheckmate)
			{
				HudManager.Instance.ShowPopUp(colorName + " won by checkmate.");
			}
			else if (results == Chess.EnumResults.WinTimeout)
			{
				for (int i = 0; i < AllCustomPlayers.Count; i++) if (winnerId != AllCustomPlayers[i].PlayerId) AllCustomPlayers[i].Timer = 0f;
				HudManager.Instance.ShowPopUp(colorName + " won by timeout.");
			}
			else if (results == Chess.EnumResults.WinResignation)
			{
				HudManager.Instance.ShowPopUp(colorName + " won by resignation.");
			}
			else
			{
				HudManager.Instance.ShowPopUp("The game mysteriously ended.");
			}

			Buttons.ClearAllHighlighted();
			End.WinnerId = (int)results < 16 ? -1 : winnerId;
		}

		public static void PlayMove(GameObject fromObject, (int x, int y) toCoordinates, GameObject toObject, Chess.EnumMoves howMove, int captures)
		{
			PlayerControl fromController = fromObject.GetComponent<PlayerControl>();
			PlayerControl toController = toObject.GetComponent<PlayerControl>();
			int nameIndexFrom = fromObject.name.IndexOf(':');
			int nameIndexTo = toObject.name.IndexOf(':');
			int team = char.IsUpper(fromObject.name[nameIndexFrom - 1]) ? 1 : 0;
			if (howMove == Chess.EnumMoves.Promotion)
			{
				int pieceIndex = 4;
				fromController.SetHat(Utils.PieceHats[pieceIndex].ToString(), fromController.scannerCount);
				fromController.SetSkin(Utils.PieceSkins[pieceIndex].ToString(), fromController.scannerCount);
				fromObject.name = (team == 1 ? "Q" : "q") + fromObject.name[nameIndexFrom..];
				nameIndexFrom = fromController.name.IndexOf(':');
			}
			switch (howMove)
			{
				case Chess.EnumMoves.KingCastle:
				case Chess.EnumMoves.QueenCastle:
					Chess.EnumMoves queenCastle = Chess.EnumMoves.QueenCastle;
					fromObject.name = fromObject.name[nameIndexFrom - 1] + ":" + (howMove == queenCastle ? "2" : "6") + "," + toCoordinates.y.ToString();
					toObject.name = toObject.name[nameIndexTo - 1] + ":" + (howMove == queenCastle ? "3" : "5") + "," + toCoordinates.y.ToString();
					fromObject.transform.position = new Vector3(howMove == queenCastle ? 17f : 19f, (toCoordinates.y * -0.5f) - 10f, fromObject.transform.position.z);
					toObject.transform.position = new Vector3(howMove == queenCastle ? 17.5f : 18.5f, (toCoordinates.y * -0.5f) - 10f, toObject.transform.position.z);
					break;
				case Chess.EnumMoves.EnPassant:
					fromObject.name = fromObject.name[nameIndexFrom - 1] + ":" + toCoordinates.x.ToString() + "," + toCoordinates.y.ToString();
					toObject.name = toObject.name[nameIndexTo - 1] + ":D";
					fromObject.transform.position = new Vector3((toCoordinates.x * 0.5f) + 16, (toCoordinates.y * -0.5f) - 10f, fromObject.transform.position.z);
					toObject.transform.position = new Vector3(25f + (captures % 10 * 0.5f), (float)((team == 1 ? -12f : -14.5f) + (Math.Floor(captures / 10f) * (team == 1 ? -0.5f : 0.5f))), toObject.transform.position.z);
					break;
				default:
					fromObject.name = fromObject.name[nameIndexFrom - 1] + ":" + toCoordinates.x.ToString() + "," + toCoordinates.y.ToString();
					toObject.name = toObject.name[nameIndexTo - 1] + ":D";
					fromObject.transform.position = new Vector3((toCoordinates.x * 0.5f) + 16, (toCoordinates.y * -0.5f) - 10f, fromObject.transform.position.z);
					toObject.transform.position = new Vector3(25f + (captures % 10 * 0.5f), (float)((team == 1 ? -12f : -14.5f) + (Math.Floor(captures / 10f) * (team == 1 ? -0.5f : 0.5f))), toObject.transform.position.z);
					break;
			}
		}

		public static void PlayMove(GameObject fromObject, (int x, int y) toCoordinates, Chess.EnumMoves howMove)
		{
			PlayerControl fromController = fromObject.GetComponent<PlayerControl>();
			int nameIndex = fromController.name.IndexOf(':');
			if (howMove == Chess.EnumMoves.Promotion)
			{
				int pieceIndex = 4;
				fromController.SetHat(Utils.PieceHats[pieceIndex].ToString(), fromController.scannerCount);
				fromController.SetSkin(Utils.PieceSkins[pieceIndex].ToString(), fromController.scannerCount);
				fromObject.name = (char.IsUpper(fromObject.name[nameIndex - 1]) ? "Q" : "q") + fromObject.name[nameIndex..];
				nameIndex = fromController.name.IndexOf(':');
			}
			fromObject.name = fromObject.name[nameIndex - 1] + ":" + toCoordinates.x.ToString() + "," + toCoordinates.y.ToString();
			fromObject.transform.position = new Vector3((toCoordinates.x * 0.5f) + 16, (toCoordinates.y * -0.5f) - 10, fromObject.transform.position.z);
		}

		public static void GetAndPlayMove((int x, int y) fromCoordinates, (int x, int y) toCoordinates)
		{
			GameObject fromObject = null;
			char[,] chessBoard = Chess.Chess.ChessBoard;
			Chess.EnumMoves howMove = Chess.EnumMoves.Normal;
			int directionY = char.IsUpper(chessBoard[fromCoordinates.y, fromCoordinates.x]) ? -1 : 1;
			float halfRank = chessBoard.GetLength(0) * 0.5f;
			if (Chess.Utils.ReadablePiece(chessBoard[fromCoordinates.y, fromCoordinates.x]) == 'P' && toCoordinates.y == halfRank + (halfRank * directionY)) howMove = Chess.EnumMoves.Promotion;
			Transform piecesObject = GameObject.Find("PiecesPath").transform;
			for (int i = 0; i < piecesObject.childCount; i++)
			{
				Transform elementObject = piecesObject.GetChild(i);
				int pieceNameIndex1 = elementObject.name.IndexOf(':');
				int pieceNameIndex2 = elementObject.name.IndexOf(',');
				if (pieceNameIndex1 == -1 || pieceNameIndex2 == -1) continue;
				(int x, int y) elementCoordinates = (int.Parse(elementObject.name[(pieceNameIndex1 + 1)..pieceNameIndex2]), int.Parse(elementObject.name[(pieceNameIndex2 + 1)..]));
				if (fromCoordinates.x == elementCoordinates.x && fromCoordinates.y == elementCoordinates.y)
				{
					fromObject = elementObject.gameObject.GetComponent<PlayerControl>().gameObject;
					break;
				}
			}
			if (chessBoard[toCoordinates.y, toCoordinates.x] != '1')
			{
				howMove = Chess.Utils.GetHowMove(fromCoordinates, toCoordinates);
				GameObject toObject = PlayerControl.LocalPlayer.gameObject;
				for (int i = 0; i < piecesObject.childCount; i++)
				{
					Transform elementObject = piecesObject.GetChild(i);
					int pieceNameIndex = elementObject.name.IndexOf(':');
					if (elementObject.name[(pieceNameIndex + 1)..] == toCoordinates.x + "," + toCoordinates.y)
					{
						toObject = elementObject.gameObject;
						break;
					}
				}
				PlayMove(fromObject, toCoordinates, toObject, howMove, char.IsUpper(chessBoard[fromCoordinates.y, fromCoordinates.x]) ? Chess.Chess.numCaptures.black++ : Chess.Chess.numCaptures.white++);
			}
			else
			{
				PlayMove(fromObject, toCoordinates, howMove);
			}
		}
	}
}
