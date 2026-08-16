using HarmonyLib;
using Hazel;
using UnityEngine;
using AmongUs.Data;
using AmongChess.Rpc;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace AmongChess.Game
{
	internal class Start
	{
		[HarmonyPatch(typeof(IntroCutscene))]
		public static class IntroCutScenePatch
		{
			[HarmonyPatch(nameof(IntroCutscene.BeginCrewmate))]
			[HarmonyPostfix]
			public static void BeginCrewmatePatch(IntroCutscene __instance)
			{
				try
				{
					Game.PlayerTurn = 0;
					Game.TotalTurns = 0;
					int playerCount = Game.RealPlayerCount;
					int[] colorIds = (int[])Game.ColorIds.GetValue(playerCount - 1);
					Game.AllPlayers.Clear();
					Game.AllCustomPlayers.Clear();
					float timeAdded = Chess.Chess.MainTime != "Unlimited" ? float.Parse(Chess.Chess.MainTime) * 60 : float.MaxValue;
					for (int i = 0; i < colorIds.Length; i++)
					{
						for (int j = 0; j < PlayerControl.AllPlayerControls.Count; j++)
						{
							PlayerControl playerControl = PlayerControl.AllPlayerControls[j];
							if (playerControl == null || playerControl.Data == null || playerControl.isDummy) continue;
							if (colorIds[i] != playerControl.Data.DefaultOutfit.ColorId) continue;
							Game.AllPlayers.Add(playerControl);
							// In 2026.6.5 the local player's outfit lives in DataManager.Player.Customization;
							// Data.DefaultOutfit stays "missing" for the wardrobe-equipped cosmetics.
							bool isLocal = playerControl == PlayerControl.LocalPlayer;
							CustomPlayer customPlayer = new CustomPlayer()
							{
								PlayerId = playerControl.PlayerId,
								HatId = isLocal ? DataManager.Player.Customization.Hat : playerControl.Data.DefaultOutfit.HatId,
								SkinId = isLocal ? DataManager.Player.Customization.Skin : playerControl.Data.DefaultOutfit.SkinId,
								PetId = isLocal ? DataManager.Player.Customization.Pet : playerControl.Data.DefaultOutfit.PetId,
								Timer = timeAdded,
								Activity = EnumActivity.GameWaiting
							};
							Game.AllCustomPlayers.Add(customPlayer);
						}
					}
					int color = PlayerControl.LocalPlayer.Data != null ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId : (int)DataManager.Player.Customization.Color;
					int index = -1;
					string otherTeams = "";
					for (int i = 0; i < Game.AllPlayers.Count; i++) if (Game.AllPlayers[i].PlayerId == PlayerControl.LocalPlayer.PlayerId) index = i;
					if (index < 0)
					{
						// Local player's color wasn't synced/matched yet → append safely so colorIds[index] never goes out of range.
						Game.AllPlayers.Add(PlayerControl.LocalPlayer);
						index = Game.AllPlayers.Count - 1;
						Game.AllCustomPlayers.Add(new CustomPlayer()
						{
							PlayerId = PlayerControl.LocalPlayer.PlayerId,
							HatId = DataManager.Player.Customization.Hat,
							SkinId = DataManager.Player.Customization.Skin,
							PetId = DataManager.Player.Customization.Pet,
							Timer = timeAdded,
							Activity = EnumActivity.GameWaiting
						});
					}
					for (int i = 0; i < playerCount - 1; i++)
					{
						if (i == index) continue;
						if (i < colorIds.Length) otherTeams = otherTeams + ", " + Game.ColorNames[colorIds[i]].ToString().ToLower();
					}
					if (otherTeams.Length == 0)
					{
						otherTeams = "the other";
					}
					else
					{
						otherTeams = otherTeams[2..];
						int teamsIndex = otherTeams.LastIndexOf(',');
						if (teamsIndex != -1) _ = otherTeams.Insert(teamsIndex + 1, " and");
						if (teamsIndex != -1 && otherTeams.LastIndexOf(',') == otherTeams.IndexOf(',')) _ = otherTeams.Remove(teamsIndex);
						otherTeams += "'s";
					}
					if (__instance.TeamTitle != null) __instance.TeamTitle.text = Game.ColorNames[colorIds[index]].ToString();
					if (__instance.TeamTitle != null) __instance.TeamTitle.color = Palette.PlayerColors[color];
					if (__instance.ImpostorText != null) __instance.ImpostorText.text = "Checkmate " + otherTeams + " king.";
					if (__instance.BackgroundBar != null) __instance.BackgroundBar.material.color = Palette.PlayerColors[color];
					Game.LocalActivity = playerCount == 1 ? EnumActivity.GameSelect : EnumActivity.GameWaiting;
					MessageWriter rpcMessageTime = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.PlayerLoaded, SendOption.Reliable, -1);
					rpcMessageTime.Write(PlayerControl.LocalPlayer.PlayerId);
					AmongUsClient.Instance.FinishRpcImmediately(rpcMessageTime);
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] Intro cutscene patch failed: " + e);
				}
			}

			[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
			[HarmonyPrefix]
			public static bool CoBeginPatch(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
			{
				__result = CustomCoBegin(__instance).WrapToIl2Cpp();
				return false;
			}

			private static System.Collections.IEnumerator CustomCoBegin(IntroCutscene __instance)
			{
				SoundManager.Instance.PlaySound(__instance.IntroStinger, false, 1f, null);
				if (GameManager.Instance.IsNormal())
				{
					__instance.HideAndSeekPanels.SetActive(false);
					__instance.CrewmateRules.SetActive(false);
					__instance.ImpostorRules.SetActive(false);
					__instance.ImpostorName.gameObject.SetActive(false);
					__instance.ImpostorTitle.gameObject.SetActive(false);
					// Rebuild the team list ourselves (SelectTeamToShow takes Il2CppSystem.Func/List
					// which are awkward to call from C#): local player first, then every real,
					// connected player. Our games are pure crewmate so every real player is shown.
					Il2CppSystem.Collections.Generic.List<PlayerControl> teamToShow = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
					PlayerControl localPlayer = PlayerControl.LocalPlayer;
					if (localPlayer != null && localPlayer.Data != null) teamToShow.Add(localPlayer);
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (pc == null || pc == localPlayer || pc.isDummy || pc.Data == null || pc.Data.Disconnected) continue;
						teamToShow.Add(pc);
					}
					if (teamToShow.Count < 1)
					{
						UnityEngine.Debug.LogError("[AmongChess] Intro teamToShow is EMPTY or NULL");
					}
					if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
					{
						__instance.ImpostorText.gameObject.SetActive(false);
					}
					else
					{
						int adjustedNumImpostors = GameManager.Instance.LogicOptions.GetAdjustedNumImpostors(GameData.Instance.PlayerCount);
						if (adjustedNumImpostors == 1)
						{
							__instance.ImpostorText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsS, new Il2CppSystem.Object[0]);
						}
						else
						{
							__instance.ImpostorText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsP, new Il2CppSystem.Object[] { adjustedNumImpostors });
						}
						__instance.ImpostorText.text = __instance.ImpostorText.text.Replace("[FF1919FF]", "<color=#FF1919FF>");
						__instance.ImpostorText.text = __instance.ImpostorText.text.Replace("[]", "</color>");
					}
					yield return __instance.ShowTeam(teamToShow, 3f);
					// ShowRole intentionally skipped
				}
				else
				{
					// Hide and Seek is not supported by Among Chess — finish the intro immediately.
					yield return null;
				}
				ShipStatus.Instance.StartSFX();
				UnityEngine.Object.Destroy(__instance.gameObject);
				yield break;
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
		public static class ShipStatusPatch
		{
			public static void Postfix()
			{
				// Keep the chat visible during the chess game so players can talk.
				if (HudManager.Instance != null && HudManager.Instance.Chat != null) HudManager.Instance.Chat.SetVisible(true);
				HudManager.Instance.ShowTaskComplete();
				Game.PieceCoords.Clear();
				string shipDirectory = "PolusShip(Clone)/";
				string[] activeObjects = new string[] { "Storage", "Outside/ScienceBuildingVent", "Outside/ElectricBuildingVent", "Outside/SouthVent", "Comms/CommsVent", "LifeSupport/ElecFenceVent", "Science/SubBathroomVent", "Outside/panel_node_ca", "Outside/panel_node_tb", "Outside/panel_node_mlg", "Outside/panel_node_gi", "Outside/panel_node_iro", "Outside/panel_node_pd", "Outside/RocksNBoxes/bigRock" };
				string[] interactiveObjects = new string[] { "Outside/panel_temphot", "Dropship/panel_fuel", "Dropship/panel_fuel (1)", "Dropship/panel_keys", "Dropship/panel_nav" };
				string[] doorObjects = new string[] { "Comms/Walls/BottomDoor", "Weapons/Walls/BottomDoor", "LifeSupport/BottomDoor", "Electrical/RightDoor", "Office/RightDoor", "Admin/LeftDoor", "Science/RightDoor" };
				for (int i = 0; i < activeObjects.Length; i++)
				{
					GameObject obj = GameObject.Find(shipDirectory + activeObjects[i]);
					if (obj != null) obj.active = false;
				}
				for (int i = 0; i < interactiveObjects.Length; i++)
				{
					GameObject obj = GameObject.Find(shipDirectory + interactiveObjects[i]);
					if (obj == null) continue;
					BoxCollider2D collider = obj.GetComponent<BoxCollider2D>();
					if (collider != null) collider.enabled = false;
				}
				for (int i = 0; i < doorObjects.Length; i++)
				{
					PlainDoor plainDoor = GameObject.Find(shipDirectory + doorObjects[i])?.GetComponent<PlainDoor>();
					if (plainDoor == null) continue;
					plainDoor.Open = true;
					plainDoor.SetDoorway(false);
					Vector2 size = plainDoor.myCollider.size;
					if (size.x > size.y)
					{
						size.x = 0.7f;
						size.y = 1.5f;
					}
					else
					{
						size.x = 0.4f;
						size.y = 2f;
					}
					plainDoor.myCollider.size = new Vector2(size.x, size.y);
				}
				GameObject ventPath = new GameObject("VentPath");
				GameObject piecesPath = new GameObject("PiecesPath");
				Chess.Chess.ChessBoard = new char[,] { { '0' } };
				Chess.Chess.SetSettings();
				char[,] chessBoard = Chess.Utils.ReadableBoard(Chess.Chess.ChessBoard);
				int[] allColors = (int[])Game.ColorIds.GetValue(Game.RealPlayerCount - 1);
				// All clients: build the 8x8 board cells (vents)
				for (int y = 0; y < 8; y++)
				{
					for (int x = 0; x < 8; x++)
					{
						try
						{
							Vent ventPrefab = Object.FindObjectOfType<Vent>();
							if (ventPrefab == null) continue;
							Vent ventControl = Object.Instantiate(ventPrefab, ventPath.transform);
							ventControl.transform.position = new Vector3((x * 0.5f) + 16, (y * -0.5f) - 10.31f, ventPrefab.transform.position.z);
							ventControl.name = x.ToString() + "," + y.ToString();
						}
						catch (System.Exception e)
						{
							UnityEngine.Debug.LogError("[AmongChess] Failed to spawn cell at (" + x + "," + y + "): " + e);
						}
					}
				}

				for (int y = 0; y < 8; y++)
				{
					for (int x = 0; x < 8; x++)
					{
						try
						{
							if (chessBoard[y, x] == '1') continue;
							if (AmongUsClient.Instance.PlayerPrefab == null) continue;
							int pieceIndex = Utils.PieceIndex(chessBoard[y, x]);
							PlayerControl playerPrefab = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, piecesPath.transform);
							PlayerControl playerControl = playerPrefab.gameObject.GetComponent<PlayerControl>();
							if (playerControl == null) continue;
							playerControl.PlayerId = (byte)GameData.Instance.GetAvailableId();
							playerControl.isDummy = true;
							playerControl.isNew = false; // don't play lobby spawn animation / don't get moved to spawn
							playerControl.SetKinematic(true); // don't let physics move the piece
							playerControl.transform.position = new Vector3((x * 0.5f) + 16, (y * -0.5f) - 10, PlayerControl.LocalPlayer != null ? PlayerControl.LocalPlayer.transform.position.z : 0f);
							Game.PieceCoords[playerControl] = ((byte)x, (byte)y);
							DummyBehaviour dummy = playerControl.GetComponent<DummyBehaviour>();
							if (dummy != null) dummy.enabled = true;
							if (playerControl.NetTransform != null) playerControl.NetTransform.enabled = false;
							playerControl.gameObject.name = chessBoard[y, x] + ":" + x.ToString() + "," + y.ToString();
							// Local GameData entry so the piece's own PlayerControl.Start() coroutine
							// ("Timeout while waiting for player data containers") doesn't disconnect us.
							GameData.Instance.AddDummy(playerControl);
							PlayerControl.AllPlayerControls.Remove(playerControl);
							int team = char.IsUpper(chessBoard[y, x]) ? 0 : 1;
							if (team > allColors.Length - 1) team = 0;
							// Complete the piece's data: the piece's ClientInitialize() coroutine reads
							// DefaultOutfit and would otherwise call SetColor(-1) (the "colorId invalid (-1)"
							// spam) and wait forever on IsIncomplete ("Timeout while waiting for other player
							// data" disconnect).
							NetworkedPlayerInfo pieceData = playerControl.Data;
							if (pieceData != null)
							{
								pieceData.PlayerLevel = 0;
								// Keep the full piece name ("P:0,0") as PlayerName: vanilla ClientInitialize calls
								// SetName(PlayerName) which overwrites gameObject.name — keeping the coords in it
								// preserves name-based piece detection/parsing everywhere.
								pieceData.DefaultOutfit.PlayerName = chessBoard[y, x].ToString() + ":" + x.ToString() + "," + y.ToString();
								pieceData.DefaultOutfit.ColorId = allColors[team];
								pieceData.DefaultOutfit.HatId = Utils.PieceHats[pieceIndex].ToString();
								pieceData.DefaultOutfit.SkinId = Utils.PieceSkins[pieceIndex].ToString();
								pieceData.DefaultOutfit.PetId = "pet_EmptyPet";
								pieceData.DefaultOutfit.VisorId = "visor_EmptyVisor";
								pieceData.DefaultOutfit.NamePlateId = "nameplate_NoPlate";
								// Mark as disconnected so IntroCutscene.SelectTeamToShow (which filters
								// `!pcd.Disconnected`) excludes the piece from the intro lineup. Kept local
								// (never synced, not in AllPlayerControls) so nothing else reacts to it.
								pieceData.Disconnected = true;
							}
							playerControl.scannerCount = (byte)team;
							if (playerControl.cosmetics != null)
							{
								// Direct render-color (no GameData → no SetColor which waits for Data).
								playerControl.cosmetics.SetColor(allColors[team]);
								if (playerControl.cosmetics.nameText != null)
									playerControl.cosmetics.nameText.color = new Color(1f, 1f, 1f, 0f);
							}
							playerControl.SetHat(Utils.PieceHats[pieceIndex].ToString(), allColors[team]);
							playerControl.SetSkin(Utils.PieceSkins[pieceIndex].ToString(), allColors[team]);
							playerControl.SetPet("", allColors[team]);
						}
						catch (System.Exception e)
						{
							UnityEngine.Debug.LogError("[AmongChess] Failed to spawn piece at (" + x + "," + y + "): " + e);
						}
					}
				}
				UnityEngine.Debug.Log("[AmongChess] Pieces done: GameData=" + (GameData.Instance != null ? GameData.Instance.AllPlayers.Count : -1) + " AllPC=" + PlayerControl.AllPlayerControls.Count + " Clients=" + (AmongUsClient.Instance != null ? AmongUsClient.Instance.allClients.Count : -1));
			}
		}

		[HarmonyPatch(typeof(PlainDoor))]
		public static class PlainDoorPatch
		{
			[HarmonyPatch(nameof(PlainDoor.Start))]
			[HarmonyPostfix]
			public static void Start(PlainDoor __instance)
			{
				__instance.Open = true;
			}
		}
	}
}
