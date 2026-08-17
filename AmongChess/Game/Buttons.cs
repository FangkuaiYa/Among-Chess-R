using HarmonyLib;
using Hazel;
using UnityEngine;

namespace AmongChess.Game
{
	internal class Buttons
	{
		[HarmonyPatch(typeof(HudManager))]
		public static class HudManagerUpdatePatch
		{
			[HarmonyPatch(nameof(HudManager.Update))]
			[HarmonyPrefix]
			public static void Postfix(HudManager __instance)
			{
				__instance.MapButton.gameObject.active = false;
				// The pet button takes over the Use button slot — hide it AND force UseButton visible,
				// otherwise HudManager keeps re-hiding UseButton when a pet is equipped.
				if (__instance.PetButton != null) __instance.PetButton.gameObject.active = false;
				if (__instance.UseButton != null)
				{
					__instance.UseButton.gameObject.active = true;
					// Drive the Use button state every frame. Vanilla's ToggleUseAndPetButton skips
					// UseButton.SetTarget entirely when the local player has a pet, so relying only on
					// the SetTarget prefix left the button stuck in its grey/disabled state.
					UseButtonManagerPatch.UpdateUseButton(__instance.UseButton);
				}
			}

			[HarmonyPatch(nameof(HudManager.ToggleUseAndPetButton))]
			[HarmonyPrefix]
			public static bool ToggleUseAndPetButton(HudManager __instance, IUsable useTarget, bool canPlayNormally, bool canPet)
			{
				if (__instance.UseButton != null) __instance.UseButton.gameObject.active = true;
				if (__instance.PetButton != null) __instance.PetButton.gameObject.active = false;
				return false;
			}

			[HarmonyPatch(nameof(HudManager.ToggleMapVisible))]
			[HarmonyPrefix]
			public static bool Prefix()
			{
				return false;
			}
		}

		[HarmonyPatch(typeof(ReportButton))]
		public static class ReportButtonManagerPatch
		{
			[HarmonyPatch(nameof(ReportButton.SetActive))]
			[HarmonyPrefix]
			public static bool SetActivePatch(ReportButton __instance)
			{
				__instance.gameObject.active = false;
				return false;
			}
		}

		[HarmonyPatch(typeof(UseButton))]
		public static class UseButtonManagerPatch
		{
			public static int num = 0;
			public static EnumActivity lastActivity = (EnumActivity)(-1);
			public static bool lastEnable = false;

			public static void ActivateButton(UseButton instance)
			{
				if (instance == null) return;
				instance.gameObject.active = true;
				try
				{
					if (instance.UseSettings != null)
					{
						for (int i = 0; i < instance.UseSettings.Length; i++)
						{
							UseButtonSettings s = instance.UseSettings[i];
							if (s == null || s.ButtonType != ImageNames.UseButton) continue;
							if (instance.graphic != null && s.Image != null) instance.graphic.sprite = s.Image;
							if (instance.buttonLabelText != null)
							{
								if (s.FontMaterial != null) instance.buttonLabelText.fontSharedMaterial = s.FontMaterial;
								instance.buttonLabelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(s.Text, new Il2CppSystem.Object[0]);
							}
							break;
						}
					}
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] ActivateButton settings failed: " + e);
				}
				instance.SetEnabled();
				// Belt-and-suspenders: force the icon fully colored/desaturated directly too.
				if (instance.graphic != null)
				{
					instance.graphic.color = Color.white;
					instance.graphic.material.SetFloat("_Desat", 0f);
				}
				if (instance.buttonLabelText != null) instance.buttonLabelText.color = Color.white;
			}

			public static void DeactivateButton(UseButton instance)
			{
				if (instance == null) return;
				instance.SetDisabled();
			}

			public static void UpdateUseButton(UseButton instance)
			{
				if (instance == null) return;
				try
				{
					if (PlayerControl.LocalPlayer != null && Game.AllPlayers.Count > 0 && Game.PlayerTurn < Game.AllPlayers.Count)
					{
						PlayerControl current = Game.AllPlayers[Game.PlayerTurn];
						EnumActivity act = Game.LocalActivity;
						if (current != null && current.PlayerId == PlayerControl.LocalPlayer.PlayerId &&
							act != EnumActivity.GameSelect && act != EnumActivity.GamePlace && act != EnumActivity.GameEnd)
						{
							UnityEngine.Debug.Log("[AmongChess] Self-correct activity " + act + " → GameSelect (my turn)");
							Game.LocalActivity = EnumActivity.GameSelect;
						}
					}
				}
				catch (System.Exception) { }
				// Diagnostic: log only when the activity state changes.
				if (Game.LocalActivity != lastActivity)
				{
					lastActivity = Game.LocalActivity;
					UnityEngine.Debug.Log("[AmongChess] UseButton LocalActivity=" + Game.LocalActivity);
				}
				if (Game.LocalActivity == EnumActivity.Lobby)
				{
					DeactivateButton(instance);
					return;
				}
				if (Game.LocalActivity == EnumActivity.GameEnd)
				{
					ActivateButton(instance);
					return;
				}
				num++;
				if (num > 3)
				{
					num = 0;
					lastEnable = false;
					GameObject piecesObjects = GameObject.Find("PiecesPath");
					if (piecesObjects != null)
					{
						ClearAllHighlighted();
						if (Game.LocalActivity == EnumActivity.GameSelect)
						{
							int[] colorIds = (int[])Game.ColorIds.GetValue(Game.RealPlayerCount - 1);
							int colorId = 0;
							if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null)
							{
								for (int i = 0; i < colorIds.Length; i++) if (colorIds[i] == PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId) { colorId = i; break; }
							}
							PlayerControl target = Utils.ClosestPiece(PlayerControl.LocalPlayer, colorId, out float distance);
							if (distance < 1 && target != null)
							{
								lastEnable = true;
								Transform spriteTransform = target.gameObject.transform.FindChild("Sprite");
								if (spriteTransform != null)
								{
									SpriteRenderer renderer = spriteTransform.gameObject.GetComponent<SpriteRenderer>();
									renderer.GetMaterial().SetFloat("_Outline", 1f);
									renderer.GetMaterial().SetColor("_OutlineColor", Color.yellow);
								}
							}
						}
						else if (Game.LocalActivity == EnumActivity.GamePlace)
						{
							Vent target = Utils.ClosestVent(PlayerControl.LocalPlayer, out float distance);
							if (distance < 1 && target != null)
							{
								lastEnable = true;
								SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
								renderer.GetMaterial().SetFloat("_Outline", 1);
								renderer.GetMaterial().SetColor("_OutlineColor", Color.yellow);
							}
						}
						else
						{
							lastEnable = false;
						}
					}
				}
				// Apply the visual state every frame from the cached result.
				if (lastEnable) ActivateButton(instance); else DeactivateButton(instance);
			}

			[HarmonyPatch(nameof(UseButton.SetTarget))]
			[HarmonyPrefix]
			public static bool SetTarget(UseButton __instance)
			{
				return false;
			}

			[HarmonyPatch(nameof(UseButton.DoClick))]
			[HarmonyPrefix]
			public static bool DoClick()
			{
				if (Game.LocalActivity == EnumActivity.Lobby)
				{
					return true;
				}
				else if (Game.LocalActivity == EnumActivity.GamePlace)
				{
					Vent target = Utils.ClosestVent(PlayerControl.LocalPlayer, out float distance);
					PlayerControl oldPlayer = PlayerControl.LocalPlayer;
					PlayerControl localPlayer = PlayerControl.LocalPlayer;
					Transform piecesObject = GameObject.Find("PiecesPath").transform;
					for (int i = 0; i < piecesObject.childCount; i++)
					{
						Transform elementObject = piecesObject.GetChild(i);
						if (elementObject.name[0] == 't')
						{
							oldPlayer = elementObject.gameObject.GetComponent<PlayerControl>();
							break;
						}
					}
					int targetNameIndex = target.name.IndexOf(',');
					int pieceNameIndex1 = oldPlayer.name.IndexOf(':');
					int pieceNameIndex2 = oldPlayer.name.IndexOf(',');
					(int x, int y) targetCoordinates = (x: int.Parse(target.name[..targetNameIndex]), y: int.Parse(target.name[(targetNameIndex + 1)..]));
					(int x, int y) pieceCoordinates = (x: int.Parse(oldPlayer.name[(pieceNameIndex1 + 1)..pieceNameIndex2]), y: int.Parse(oldPlayer.name[(pieceNameIndex2 + 1)..]));
					int playerIndex = Utils.FindIndexById(PlayerControl.LocalPlayer.PlayerId);
					CustomPlayer customPlayer = Game.AllCustomPlayers[playerIndex];
					if (targetCoordinates.x == pieceCoordinates.x && targetCoordinates.y == pieceCoordinates.y)
					{
						Utils.RevertMove(playerIndex, oldPlayer);
						return false;
					}
				  Chess.EnumResults move = Chess.Chess.MovePiece(pieceCoordinates, targetCoordinates, oldPlayer.gameObject);
					if (move == Chess.EnumResults.ErrorInvalid) return false;
					Utils.RevertClothing(playerIndex);
					if (Game.TotalTurns % 10 == 0 && Game.TotalTurns > 0) Utils.SynchronizeTime(customPlayer.Timer);
					Utils.SendCoordinates(pieceCoordinates, targetCoordinates);
					MessageWriter rpcMessageReturn = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)Rpc.EnumRpc.ReturnPiece, (SendOption)1);
					rpcMessageReturn.Write(localPlayer.PlayerId);
					AmongUsClient.Instance.FinishRpcImmediately(rpcMessageReturn);
					target.GetComponent<SpriteRenderer>().GetMaterial().SetFloat("_Outline", 0);
					oldPlayer.gameObject.active = true;
					Utils.AddIncrementTime(playerIndex);
					Game.LocalActivity = EnumActivity.GameWaiting;
					Utils.IncrementTurn();
					if ((int)move < 32)
					{
						Game.EventEnded(move, PlayerControl.LocalPlayer.PlayerId, true);
						return false;
					}
				}
				else if (Game.LocalActivity == EnumActivity.GameSelect)
				{
					try
					{
						if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return false;
						int[] colorIds = (int[])Game.ColorIds.GetValue(Game.RealPlayerCount - 1);
						int colorId = 0;
						for (int i = 0; i < colorIds.Length; i++) if (colorIds[i] == PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId) { colorId = i; break; }
						PlayerControl targetPlayer = Utils.ClosestPiece(PlayerControl.LocalPlayer, colorId, out float distance);
						if (distance > 1 || targetPlayer == null) return false;
						PlayerControl localPlayer = PlayerControl.LocalPlayer;
						Transform sprite = targetPlayer.transform.FindChild("Sprite");
						if (sprite != null) sprite.GetComponent<SpriteRenderer>().GetMaterial().SetFloat("_Outline", 0);
						int pieceIndex = Utils.PieceIndex(targetPlayer.name[0]);
						targetPlayer.gameObject.active = false;
						localPlayer.transform.position = targetPlayer.transform.position;
						localPlayer.SetHat(Utils.PieceHats[pieceIndex].ToString(), localPlayer.Data.DefaultOutfit.ColorId);
						localPlayer.SetSkin(Utils.PieceSkins[pieceIndex].ToString(), localPlayer.Data.DefaultOutfit.ColorId);
						localPlayer.SetPet("");
						MessageWriter rpcMessage = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)Rpc.EnumRpc.SelectPiece, (SendOption)1);
						rpcMessage.Write(localPlayer.PlayerId);
						rpcMessage.Write((byte)pieceIndex);
						AmongUsClient.Instance.FinishRpcImmediately(rpcMessage);
						targetPlayer.name = "t" + targetPlayer.name;
						Game.LocalActivity = EnumActivity.GamePlace;
					}
					catch (System.Exception e)
					{
						UnityEngine.Debug.LogError("[AmongChess] DoClick GameSelect failed: " + e);
					}
				}
				else if (Game.LocalActivity == EnumActivity.GameEnd)
				{
					if (AmongUsClient.Instance.AmHost)
					{
						GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByVote, false);
					}
					else
					{
						MessageWriter rpcMessage = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)Rpc.EnumRpc.GameEnd, (SendOption)1);
						AmongUsClient.Instance.FinishRpcImmediately(rpcMessage);
					}
				}
				else if (Game.LocalActivity == EnumActivity.GameWaiting)
				{
					return false;
				}
				return false;
			}
		}

		public static void ClearAllHighlighted()
		{
			GameObject piecesObjects = GameObject.Find("PiecesPath");
			if (piecesObjects != null)
			{
				for (int i = 0; i < piecesObjects.transform.childCount; i++)
				{
					Transform sprite = piecesObjects.transform.GetChild(i).FindChild("Sprite");
					if (sprite == null) continue;
					SpriteRenderer renderer = sprite.GetComponent<SpriteRenderer>();
					renderer.GetMaterial().SetFloat("_Outline", 0f);
				}
			}
			GameObject ventObjects = GameObject.Find("VentPath");
			if (ventObjects != null)
			{
				for (int i = 0; i < ventObjects.transform.childCount; i++)
				{
					GameObject elementObject = ventObjects.transform.GetChild(i).gameObject;
					SpriteRenderer renderer = elementObject.GetComponent<SpriteRenderer>();
					renderer.GetMaterial().SetFloat("_Outline", 0f);
				}
			}
		}
	}
}