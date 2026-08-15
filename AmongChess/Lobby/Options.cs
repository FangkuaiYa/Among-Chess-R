using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using AmongChess.Rpc;
using AmongUs.GameOptions;
using Reactor.Localization.Utilities;

namespace AmongChess.Lobby
{
	internal class Options
	{
		public static List<ClassOption> AllOption = new List<ClassOption>();
		public static List<ClassOptionGroup> AllOptionGroup = new List<ClassOptionGroup>();

		[HarmonyPatch(typeof(GameOptionsManager))]
		public static class GameOptionsManagerInitializePatch
		{
			[HarmonyPatch(nameof(GameOptionsManager.Initialize))]
			[HarmonyPostfix]
			public static void InitializePatch()
			{
				AllOptionGroup = OptionGroupDefault();
				AllOption = Save.GameOptionsImport();
			}
		}

		[HarmonyPatch(typeof(PlayerControl))]
		private class PlayerControlPatch
		{
			[HarmonyPatch(nameof(PlayerControl.RpcSyncSettings))]
			[HarmonyPostfix]
			public static void RpcSyncSettingsPatch()
			{
				if (PlayerControl.AllPlayerControls.Count > 1 && AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
				{
					MessageWriter rpcMessage = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.CustomOptions, SendOption.Reliable, -1);
					for (int i = 0; i < AllOption.Count; i++)
					{
						rpcMessage.Write(AllOption[i].Id);
						rpcMessage.Write(AllOption[i].Value);
					}
					AmongUsClient.Instance.FinishRpcImmediately(rpcMessage);
				}
			}
		}

		public static List<ClassOption> OptionDefault()
		{
			ClassOption gameMode = new ClassOption()
			{
				Id = 0,
				Name = "Game Mode",
				AllValues = new string[] { "Chess" }
			};
			ClassOption variant = new ClassOption()
			{
				Id = 1,
				Name = "Variant",
				AllValues = new string[] { "Normal", "Real-Time" }
			};
			ClassOption board = new ClassOption()
			{
				Id = 2,
				Name = "Board",
				AllValues = new string[] { "Default", "Chess960", "Transcendental"}
			};
			ClassOption mainTime = new ClassOption()
			{
				Id = 3,
				Name = "Main Time",
				AllValues = new string[] { "Unlimited", "0.5", "1", "2", "3", "5", "10", "30", "60" },
				GroupId = 0
			};
			ClassOption incrementalTime = new ClassOption()
			{
				Id = 4,
				Name = "Increment Time",
				AllValues = new string[] { "0", "0.5", "1", "2", "5", "10", "30" },
				GroupId = 0,
			};
			return new List<ClassOption> { gameMode, variant, board, mainTime, incrementalTime }; ;
		}

		public static List<ClassOptionGroup> OptionGroupDefault()
		{
			List<ClassOptionGroup> allOptionGroup = new List<ClassOptionGroup> { };
			ClassOptionGroup timeControl = new ClassOptionGroup()
			{
				Id = 0,
				Name = "Time Control",
				Value = "{3} + {4}",
			};
			allOptionGroup.Add(timeControl);
			return allOptionGroup;
		}

		public static void RpcPushDeltaOptions(byte optionId, uint optionValue)
		{
			Save.GameOptionsExport();
			MessageWriter rpcMessage = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.CustomOptions, SendOption.Reliable, -1);
			rpcMessage.Write(optionId);
			rpcMessage.Write((byte)optionValue);
			AmongUsClient.Instance.FinishRpcImmediately(rpcMessage);
		}

		// Show a toast notification when a custom setting changes (like the vanilla "X changed setting" message)
		public static void NotifyOptionChanged(ClassOption option)
		{
			if (DestroyableSingleton<HudManager>.Instance == null || DestroyableSingleton<HudManager>.Instance.Notifier == null) return;
			if (option.StringName == (StringNames)0) option.StringName = CustomStringName.CreateAndRegister(option.Name);
			DestroyableSingleton<HudManager>.Instance.Notifier.AddSettingsChangeMessage(option.StringName, option.AllValues[option.Value], false, RoleTypes.Crewmate);

			// Our custom options never write into GameOptionsManager.CurrentGameOptions, so the vanilla
			// refresh chain (GameStartManager.CheckSettingsDiffs → LobbyInfoPane.RefreshPane) never fires.
			// Manually refresh the read-only view page so it shows the updated value right away.
			if (DestroyableSingleton<LobbyInfoPane>.InstanceExists && DestroyableSingleton<LobbyInfoPane>.Instance != null)
			{
				DestroyableSingleton<LobbyInfoPane>.Instance.RefreshPane();
			}
		}
	}
}
