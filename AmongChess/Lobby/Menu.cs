using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AmongChess.Lobby
{
	[HarmonyPatch]
	public class Menu
	{
		private const int MaskLayer = 20;
		private const float StartPosY = 0.713f;
		private const float HeaderX = -0.903f;
		private const float HeaderScale = 0.63f;
		private const float HeaderSpacing = 0.63f;
		private const float OptionX = 0.952f;
		private const float OptionSpacing = 0.45f;
		private const float MapPickerHeight = 1.65f;
		private const string HeaderName = "AmongChessHeader";
		private const string OptionNamePrefix = "AmongChessOption";
		private const string ViewHeaderName = "AmongChessViewHeader";

		// Intercept the whole settings menu container:
		// - hide the vanilla "Presets" & "Role Settings" tabs
		// - open directly on the "Game Settings" tab (which we replace with our own options)
		[HarmonyPatch(typeof(GameSettingMenu))]
		public static class GameSettingMenuPatch
		{
			[HarmonyPostfix]
			[HarmonyPatch(nameof(GameSettingMenu.OnEnable))]
			public static void OnEnablePostfix(GameSettingMenu __instance)
			{
					try
					{
						if (__instance.GamePresetsButton != null) __instance.GamePresetsButton.gameObject.SetActive(false);
						if (__instance.RoleSettingsButton != null) __instance.RoleSettingsButton.gameObject.SetActive(false);
						// Open the Game Settings tab (our custom page) instead of the vanilla Presets tab
						__instance.ChangeTab(1, false);
					}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] GameSettingMenu patch failed: " + e);
				}
			}
		}

		[HarmonyPatch(typeof(GameOptionsMenu))]
		public static class GameOptionsMenuPatch
		{
			[HarmonyPrefix]
			[HarmonyPatch(nameof(GameOptionsMenu.OnEnable))]
			public static bool OnEnablePrefix(GameOptionsMenu __instance)
			{
				try
				{
					BuildCustomSettings(__instance);
					return false;
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] OnEnable patch failed: " + e);
					return true;
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(GameOptionsMenu.CreateSettings))]
			public static bool CreateSettingsPrefix(GameOptionsMenu __instance)
			{
				try
				{
					BuildCustomSettings(__instance);
					return false;
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] CreateSettings patch failed: " + e);
					return true;
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(GameOptionsMenu.Update))]
			public static bool UpdatePrefix()
			{
				// We manage our own options → skip the vanilla refresh (it would read option.Data which is null)
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(GameOptionsMenu.OpenMenu))]
			public static bool OpenMenuPrefix()
			{
				// The vanilla OpenMenu reads the (now hidden) MapPicker → skip it, mouse users don't need it
				return false;
			}
		}

		// Intercept the read-only "View Settings" page (shown to non-hosts / viewers)
		[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.SetTab))]
		public static class LobbyViewSettingsPanePatch
		{
			[HarmonyPrefix]
			public static bool SetTabPrefix(LobbyViewSettingsPane __instance)
			{
				UnityEngine.Debug.Log("[AmongChess] LobbyViewSettingsPane.SetTab intercepted.");
				try
				{
					BuildViewSettings(__instance);
					UnityEngine.Debug.Log("[AmongChess] View settings built (" + Options.AllOption.Count + " options).");
					return false;
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] View settings patch failed: " + e);
					return true;
				}
			}
		}

		// ChangeTab: prefix so we skip the vanilla destroy+redraw (which would show the vanilla page)
		[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.ChangeTab))]
		public static class LobbyViewSettingsPaneChangeTabPatch
		{
			[HarmonyPrefix]
			public static bool ChangeTabPrefix(LobbyViewSettingsPane __instance)
			{
				try
				{
					BuildViewSettings(__instance);
					return false;
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] View settings ChangeTab patch failed: " + e);
					return true;
				}
			}
		}

		// RefreshTab: called by LobbyInfoPane.RefreshPane() whenever the host changes a setting.
		// Prefix forces a rebuild with the latest values so the view page never reverts to vanilla.
		[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.RefreshTab))]
		public static class LobbyViewSettingsPaneRefreshTabPatch
		{
			[HarmonyPrefix]
			public static bool RefreshTabPrefix(LobbyViewSettingsPane __instance)
			{
				UnityEngine.Debug.Log("[AmongChess] LobbyViewSettingsPane.RefreshTab intercepted (host changed settings).");
				try
				{
					BuildViewSettings(__instance, true);
					return false;
				}
				catch (System.Exception e)
				{
					UnityEngine.Debug.LogError("[AmongChess] View settings RefreshTab patch failed: " + e);
					return true;
				}
			}
		}

		private static void BuildViewSettings(LobbyViewSettingsPane __instance, bool force = false)
		{
			UnityEngine.Debug.Log("[AmongChess] Intercepting LobbyViewSettingsPane" + (force ? " (force rebuild)..." : "..."));
			Transform container = __instance.settingsContainer;
			ViewSettingsInfoPanel infoPanelOrigin = __instance.infoPanelOrigin;
			if (container == null || infoPanelOrigin == null)
			{
				UnityEngine.Debug.LogError("[AmongChess] View settings container/infoPanel not found!");
				return;
			}

			// Already built → keep (unless forcing a rebuild for a settings refresh)
			if (!force && container.childCount > 0 && container.GetChild(0).name == ViewHeaderName) return;

			// We only show our own options → hide the Roles tab, keep Overview
			if (__instance.rolesTabButton != null) __instance.rolesTabButton.gameObject.SetActive(false);
			if (__instance.taskTabButton != null) __instance.taskTabButton.gameObject.SetActive(true);

			// Clear any leftover content (force rebuild destroys immediately so old panels never linger)
			if (container.childCount > 0)
			{
				for (int i = container.childCount - 1; i >= 0; i--)
				{
					GameObject child = container.GetChild(i).gameObject;
					if (!child.activeSelf) continue;
					if (__instance.settingsInfo != null) __instance.settingsInfo.Remove(child);
					if (force) Object.DestroyImmediate(child);
					else Object.Destroy(child);
				}
			}

			if (Options.AllOption == null || Options.AllOption.Count == 0) Options.AllOption = Options.OptionDefault();

			float num = 1.44f;

			// Header
			CategoryHeaderMasked headerOrigin = __instance.categoryHeaderOrigin;
			if (headerOrigin != null)
			{
				CategoryHeaderMasked header = Object.Instantiate(headerOrigin);
				header.gameObject.name = ViewHeaderName;
				header.transform.SetParent(container, false);
				header.transform.localScale = Vector3.one;
				header.transform.localPosition = new Vector3(-9.77f, num, -2f);
				header.SetHeader(StringNames.GameMapName, 61);
				if (header.Title != null) header.Title.text = "Among Chess";
				if (__instance.settingsInfo != null) __instance.settingsInfo.Add(header.gameObject);
				num -= 1.05f;
			}

			// Our options (two-column layout like vanilla)
			for (int i = 0; i < Options.AllOption.Count; i++)
			{
				ClassOption classOption = Options.AllOption[i];
				ViewSettingsInfoPanel panel = Object.Instantiate(infoPanelOrigin);
				panel.transform.SetParent(container, false);
				panel.transform.localScale = Vector3.one;
				float x;
				if (i % 2 == 0)
				{
					x = -8.95f;
					if (i > 0) num -= 0.85f;
				}
				else
				{
					x = -3f;
				}
				panel.transform.localPosition = new Vector3(x, num, -2f);
				panel.SetInfo((StringNames)0, classOption.AllValues[classOption.Value], 61);
				if (panel.titleText != null) panel.titleText.text = classOption.Name;
				if (__instance.settingsInfo != null) __instance.settingsInfo.Add(panel.gameObject);
			}
			num -= 0.85f;

			if (__instance.scrollBar != null) __instance.scrollBar.CalculateAndSetYBounds((float)(__instance.settingsInfo.Count + 10), 2f, 6f, 0.85f);
			if (__instance.gameModeText != null) __instance.gameModeText.text = "Among Chess";
		}

		private static void BuildCustomSettings(GameOptionsMenu __instance)
		{
			UnityEngine.Debug.Log("[AmongChess] Intercepting GameOptionsMenu settings...");

			Transform container = __instance.settingsContainer; // direct access (field is public in game libs)
			Scroller scrollBar = __instance.scrollBar;

			// Fallback: find the scroll content directly (Scroller.Inner is public)
			if (scrollBar == null) scrollBar = __instance.GetComponentInChildren<Scroller>();
			if (container == null && scrollBar != null)
			{
				container = scrollBar.Inner;
				UnityEngine.Debug.Log("[AmongChess] Using Scroller.Inner as settings container.");
			}
			if (container == null)
			{
				UnityEngine.Debug.LogError("[AmongChess] settings container not found!");
				return;
			}

			// Already built once → keep (avoids duplicates when the tab is re-enabled)
			if (container.childCount > 0 && container.GetChild(0).name == HeaderName) return;

			// Remove any leftover/duplicate visible content so nothing overlaps
			if (container.childCount > 0)
			{
				UnityEngine.Debug.Log("[AmongChess] Clearing " + container.childCount + " leftover children from settings container.");
				for (int i = container.childCount - 1; i >= 0; i--)
				{
					GameObject child = container.GetChild(i).gameObject;
					if (child.activeSelf) Object.Destroy(child);
				}
			}

			CategoryHeaderMasked headerOrigin = __instance.categoryHeaderOrigin;
			StringOption optionOrigin = __instance.stringOptionOrigin;
			Collider2D clickMask = __instance.ButtonClickMask;
			GameOptionsMapPicker mapPicker = __instance.MapPicker;

			// Block the vanilla map picker (we only want our own settings)
			if (mapPicker != null) mapPicker.gameObject.SetActive(false);

			if (Options.AllOption == null || Options.AllOption.Count == 0) Options.AllOption = Options.OptionDefault();

			if (optionOrigin == null)
			{
				UnityEngine.Debug.LogError("[AmongChess] stringOptionOrigin not found - cannot build custom options!");
				return;
			}

			var children = new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();
			float y = StartPosY;

			// First entry: a whole category Header (for looks)
			if (headerOrigin != null)
			{
				CategoryHeaderMasked header = Object.Instantiate(headerOrigin, Vector3.zero, Quaternion.identity, container);
				header.gameObject.name = HeaderName;
				header.transform.localScale = Vector3.one * HeaderScale;
				header.transform.localPosition = new Vector3(HeaderX, y, -2f);
				header.SetHeader(StringNames.GameMapName, MaskLayer);
				TextMeshPro title = header.Title; // direct access
				if (title != null) title.text = "Among Chess";
				y -= HeaderSpacing;
			}

			// Our own settings
			for (int i = 0; i < Options.AllOption.Count; i++)
			{
				ClassOption classOption = Options.AllOption[i];
				StringOption option = Object.Instantiate(optionOrigin, Vector3.zero, Quaternion.identity, container);
				option.gameObject.name = OptionNamePrefix + classOption.Id;
				option.transform.localPosition = new Vector3(OptionX, y, -2f);
				option.SetClickMask(clickMask);
				ApplyMask(option);
				option.Values = new StringNames[classOption.AllValues.Length];
				option.Value = classOption.Value;
				option.TitleText.text = classOption.Name;
				option.ValueText.text = classOption.AllValues[classOption.Value];
				if (classOption.AllValues.Length <= 1)
				{
					// single-value option (e.g. Game Mode) → disable both +/- buttons
					if (option.MinusBtn != null) option.MinusBtn.SetInteractable(false);
					if (option.PlusBtn != null) option.PlusBtn.SetInteractable(false);
				}
				else
				{
					RefreshButtonState(option);
				}
				if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) option.SetAsPlayer();
				children.Add(option);
				y -= OptionSpacing;
			}

			if (scrollBar != null) scrollBar.SetYBoundsMax(-y - MapPickerHeight);
			if (__instance.ControllerSelectable == null) __instance.ControllerSelectable = new Il2CppSystem.Collections.Generic.List<UiElement>();
			if (scrollBar != null)
			{
				var selectables = scrollBar.GetComponentsInChildren<UiElement>();
				for (int i = 0; i < selectables.Length; i++) __instance.ControllerSelectable.Add(selectables[i]);
			}
			__instance.Children = children;

			UnityEngine.Debug.Log("[AmongChess] Custom settings menu built (" + Options.AllOption.Count + " options).");
		}

		private static void ApplyMask(StringOption option)
		{
			SpriteRenderer[] sprites = option.GetComponentsInChildren<SpriteRenderer>(true);
			for (int i = 0; i < sprites.Length; i++) sprites[i].material.SetInt(PlayerMaterial.MaskLayer, MaskLayer);
			TextMeshPro[] texts = option.GetComponentsInChildren<TextMeshPro>(true);
			for (int i = 0; i < texts.Length; i++)
			{
				texts[i].fontMaterial.SetFloat("_StencilComp", 3f);
				texts[i].fontMaterial.SetFloat("_Stencil", MaskLayer);
			}
		}

		private static void RefreshButtonState(StringOption option)
		{
			AccessTools.Method(typeof(StringOption), "AdjustButtonsActiveState")?.Invoke(option, null);
		}

		private static ClassOption FindClassOption(StringOption option)
		{
			if (option == null || !option.name.StartsWith(OptionNamePrefix)) return null;
			byte id;
			if (!byte.TryParse(option.name.Substring(OptionNamePrefix.Length), out id)) return null;
			return Options.AllOption.Find(ele => ele.Id == id);
		}

		[HarmonyPatch(typeof(StringOption))]
		public static class StringOptionPatch
		{
			[HarmonyPrefix]
			[HarmonyPatch(nameof(StringOption.Start))]
			public static bool Start(StringOption __instance)
			{
				// Skip the vanilla Initialize for our own options (they have no BaseGameSetting data)
				return FindClassOption(__instance) == null;
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(StringOption.FixedUpdate))]
			public static bool FixedUpdate(StringOption __instance)
			{
				// Skip the vanilla text refresh for our own options (we set the text manually)
				return FindClassOption(__instance) == null;
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(StringOption.Increase))]
			public static bool Increase(StringOption __instance)
			{
				ClassOption classOption = FindClassOption(__instance);
				if (classOption == null) return true;
				if (classOption.Value < classOption.AllValues.Length - 1)
				{
					classOption.Value++;
					__instance.Value = classOption.Value;
					__instance.ValueText.text = classOption.AllValues[classOption.Value];
					RefreshButtonState(__instance);
					Options.RpcPushDeltaOptions(classOption.Id, classOption.Value);
					Options.NotifyOptionChanged(classOption);
				}
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(StringOption.Decrease))]
			public static bool Decrease(StringOption __instance)
			{
				ClassOption classOption = FindClassOption(__instance);
				if (classOption == null) return true;
				if (classOption.Value > 0)
				{
					classOption.Value--;
					__instance.Value = classOption.Value;
					__instance.ValueText.text = classOption.AllValues[classOption.Value];
					RefreshButtonState(__instance);
					Options.RpcPushDeltaOptions(classOption.Id, classOption.Value);
					Options.NotifyOptionChanged(classOption);
				}
				return false;
			}
		}
	}
}
