using HarmonyLib;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using TMPro;

namespace AmongChess.Lobby
{
	[HarmonyPatch]
	public class Start
	{
		[HarmonyPatch(typeof(PingTracker))]
		internal static class PingTrackerPatch
		{
			[HarmonyPatch(nameof(PingTracker.Update))]
			[HarmonyPostfix]
			static void PingTrackerUpdate(PingTracker __instance)
			{
				try
				{
					var GameModeText = GameObject.Find("GameModeText")?.GetComponent<TextMeshPro>();
					GameModeText.text = "Chess";
					var ModeLabel = GameObject.Find("ModeLabel")?.GetComponentInChildren<TextMeshPro>();
					ModeLabel.text = "Game Mode";
				}
				catch { }
			}
		}

		[HarmonyPatch(typeof(GameStartManager))]
		public static class GameStartManagerPatch
		{
			[HarmonyPatch(nameof(GameStartManager.Start))]
			[HarmonyPrefix]
			public static void StartPatch(ref GameStartManager __instance)
			{
				Game.Game.AllCustomPlayers?.Clear();
				Game.Game.AllPlayers?.Clear();
				Game.Game.PieceCoords?.Clear();
				if (__instance != null) __instance.MinPlayers = 2;

				if (GameOptionsManager.Instance == null) return;
				try
				{
					ApplyChessOptions(GameOptionsManager.Instance.currentNormalGameOptions);
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] GameStartManager options patch failed: " + e);
				}
			}

			private static void ApplyChessOptions(NormalGameOptionsV10 options)
			{
				if (options == null) return;
				options.SetInt(Int32OptionNames.MaxPlayers, 2);
				options.SetByte(ByteOptionNames.MapId, 2);
				options.SetFloat(FloatOptionNames.CrewLightMod, 5f);
				options.SetFloat(FloatOptionNames.PlayerSpeedMod, 1f);
			}

			[HarmonyPatch(nameof(GameStartManager.BeginGame))]
			[HarmonyPrefix]
			public static bool BeginGamePatch(GameStartManager __instance)
			{
				ClassOption GameMode = Options.AllOption.Find(ele => ele.Name == "Game Mode");
				if (GameMode.AllValues[GameMode.Value] == "Dev-Chess") __instance.ReallyBegin(false);
				if (__instance.startState == GameStartManager.StartingStates.NotStarting)
				{
					if (Game.Game.RealPlayerCount < __instance.MinPlayers)
					{
						_ = __instance.StartCoroutine(Effects.SwayX(__instance.PlayerCounter.transform));
					}
					else
					{
						__instance.ReallyBegin(neverShow: false);
					}
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(RoleManager))]
		private class RoleManagerPatch
		{
			[HarmonyPatch(nameof(RoleManager.SelectRoles))]
			[HarmonyPrefix]
			public static bool SelectRoles()
			{
				if (AmongUsClient.Instance.AmHost)
				{
					try
					{
						UnityEngine.Debug.Log("[AmongChess] SelectRoles intercepted");
						Game.Game.AllPlayers = new List<PlayerControl> { };
						int playersCount = Game.Game.RealPlayerCount;
						int[] colorsArray = (int[])Game.Game.ColorIds.GetValue(playersCount - 1);
						List<int> colorsList = new List<int> { };
						for (int i = 0; i < colorsArray.Length; i++) colorsList.Add(colorsArray[i]);
						int realIndex = 0;
						for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
						{
							PlayerControl playerControl = PlayerControl.AllPlayerControls[i];
							if (playerControl == null || playerControl.isDummy) continue; // skip chess pieces
							int random = UnityEngine.Random.RandomRangeInt(0, playersCount - realIndex);
							playerControl.RpcSetColor((byte)colorsList[random]);
							colorsList.RemoveAt(random);
							realIndex++;
						}
						// Chess has no impostor — force every real player to Crewmate. RpcSetRole applies
						// locally (host) AND broadcasts to clients (via CoSetRole / RpcSetRoleMessage).
						for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
						{
							PlayerControl playerControl = PlayerControl.AllPlayerControls[i];
							if (playerControl == null || playerControl.isDummy || playerControl.Data == null || playerControl.Data.Role == null) continue;
							playerControl.RpcSetRole(RoleTypes.Crewmate);
						}
					}
					catch (System.Exception e)
					{
						UnityEngine.Debug.LogError("[AmongChess] SelectRoles patch failed: " + e);
					}
				}
				return false; // skip vanilla SelectRoles (vanilla is never allowed to assign roles)
			}

			[HarmonyPatch(nameof(RoleManager.SetRole))]
			[HarmonyPrefix]
			public static bool SetRole(PlayerControl targetPlayer, ref RoleTypes roleType)
			{
				try
				{
					if (targetPlayer == null) return false;
					if (targetPlayer.isDummy) return false; // chess piece — never assign a role
					if (RoleManager.IsGhostRole(roleType)) return true; // keep ghost roles (death flow)
					if (roleType != RoleTypes.Crewmate)
					{
						UnityEngine.Debug.Log("[AmongChess] Forcing role " + roleType + " → Crewmate for player " + targetPlayer.PlayerId);
						roleType = RoleTypes.Crewmate;
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] SetRole patch failed: " + e);
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(PlayerControl))]
		private class PlayerControlRolePatch
		{
			[HarmonyPatch(nameof(PlayerControl.CoSetRole))]
			[HarmonyPrefix]
			public static bool CoSetRole(PlayerControl __instance, ref RoleTypes role, bool canOverride)
			{
				try
				{
					if (__instance == null) return false;
					if (__instance.isDummy) return false; // chess piece — never assign a role
					if (RoleManager.IsGhostRole(role)) return true; // keep ghost roles (death flow)
					if (role != RoleTypes.Crewmate)
					{
						UnityEngine.Debug.Log("[AmongChess] CoSetRole " + role + " → Crewmate for " + __instance.PlayerId);
						role = RoleTypes.Crewmate;
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] CoSetRole patch failed: " + e);
				}
				return true;
			}
		}
	}
}
