using HarmonyLib;
using Hazel;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using AmongChess.Rpc;

namespace AmongChess.Game
{
	internal class Utils
	{
		public static char[] PieceTranslation = new char[6] { 'P', 'N', 'B', 'R', 'Q', 'K' };
		// 2026.6.5 uses string cosmetic IDs ("hat_xxx"/"skin_xxx"). Order matches PieceTranslation:
		// [0]=Pawn(兵), [1]=Knight(马), [2]=Bishop(象), [3]=Rook(车), [4]=Queen(后), [5]=King(王)
		public static string[] PieceHats = new string[6] { "hat_NoHat", "hat_pk03_Fedora", "hat_pk03_Security1", "hat_pk05_Helmet", "hat_pk02_HaloHat", "hat_pk02_Crown" };
		public static string[] PieceSkins = new string[6] { "skin_None", "skin_SuitB", "skin_Security", "skin_Archae", "skin_Science", "skin_SuitW" };

		public static PlayerControl ClosestPiece(PlayerControl referencePlayer, int color, out float minDistance)
		{
			GameObject allObjects = GameObject.Find("PiecesPath");
			minDistance = float.MaxValue;
			PlayerControl result = referencePlayer;
			for (int i = 0; i < allObjects.transform.childCount; i++)
			{
				GameObject elementObject = allObjects.transform.GetChild(i).gameObject;
				PlayerControl elementPlayer = elementObject.GetComponent<PlayerControl>();
				if (color != elementPlayer.scannerCount || elementPlayer.name.IndexOf(",") == -1) continue;
				float distance = Vector2.Distance(referencePlayer.GetTruePosition(), elementPlayer.GetTruePosition());
				if (minDistance < distance) continue;
				minDistance = distance;
				result = elementPlayer;
			}
			return result;
		}

		public static Vent ClosestVent(PlayerControl referencePlayer, out float minDistance)
		{
			GameObject allObjects = GameObject.Find("VentPath");
			minDistance = float.MaxValue;
			Vent result = null;
			for (int i = 0; i < allObjects.transform.childCount; i++)
			{
				GameObject elementObject = allObjects.transform.GetChild(i).gameObject;
				Vent elementPlayer = elementObject.GetComponent<Vent>();
				float distance = Vector2.Distance(referencePlayer.GetTruePosition(), new Vector2(elementObject.transform.position.x, elementObject.transform.position.y - 0.1f));
				if (minDistance < distance) continue;
				minDistance = distance;
				result = elementPlayer;
			}
			return result;
		}

		public static PlayerControl FindPlayer(byte playerID)
		{
			for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
			{
				PlayerControl playerControl = PlayerControl.AllPlayerControls[i];
				if (playerControl.PlayerId == playerID) return playerControl;
			}
			return null;
		}

		public static CustomPlayer FindCustom(byte playerID)
		{
			for (int i = 0; i < Game.AllCustomPlayers.Count; i++)
			{
				CustomPlayer playerControl = Game.AllCustomPlayers[i];
				if (playerControl.PlayerId == playerID) return playerControl;
			}
			return null;
		}

		public static int PieceIndex(char piece)
		{
			return Array.IndexOf(PieceTranslation, char.ToUpper(piece));
		}

		public static void RevertClothing(int index)
		{
			CustomPlayer customPlayer = Game.AllCustomPlayers[index];
			PlayerControl playerControl = Game.AllPlayers[index];
			playerControl.SetHat(customPlayer.HatId, playerControl.Data.DefaultOutfit.ColorId);
			playerControl.SetSkin(customPlayer.SkinId, playerControl.Data.DefaultOutfit.ColorId);
			playerControl.SetPet(customPlayer.PetId);
		}

		public static void RevertClothingById(byte playerId)
		{
			PlayerControl playerControl = FindPlayer(playerId);
			int index = Game.AllPlayers.FindIndex(ele => ele.PlayerId == playerControl.PlayerId);
			RevertClothing(index);
		}

		public static void IncrementTurn()
		{
			Game.PlayerTurn++;
			if (Game.AllPlayers.Count <= Game.PlayerTurn)
			{
				Game.TotalTurns++;
				Game.PlayerTurn = 0;
			}
			if (Game.AllPlayers[Game.PlayerTurn].PlayerId == PlayerControl.LocalPlayer.PlayerId)
			{
				Game.LocalActivity = EnumActivity.GameSelect;
				HudManager.Instance.ShowTaskComplete();
			}
			else
			{
				Game.LocalActivity = EnumActivity.GameWaiting;
			}
		}

		public static void AddIncrementTime(int index)
		{
			Game.AllCustomPlayers[index].Timer += 0.25f + float.Parse(Chess.Chess.IncrementTime);
		}

		public static void SynchronizeTime(float timer)
		{
			MessageWriter rpcMessageTime = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.SynchronizeTime, SendOption.Reliable, -1);
			rpcMessageTime.Write(timer);
			AmongUsClient.Instance.FinishRpcImmediately(rpcMessageTime);
		}

		public static void SendCoordinates((int x, int y) fromCoordinates, (int x, int y) toCoordinates)
		{
			MessageWriter rpcMessageMove = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.MovePiece, SendOption.Reliable, -1);
			rpcMessageMove.Write((byte)fromCoordinates.x);
			rpcMessageMove.Write((byte)fromCoordinates.y);
			rpcMessageMove.Write((byte)toCoordinates.x);
			rpcMessageMove.Write((byte)toCoordinates.y);
			AmongUsClient.Instance.FinishRpcImmediately(rpcMessageMove);
		}

		public static void RevertMove(int playerIndex, PlayerControl oldPlayer)
		{
			RevertClothing(playerIndex);
			MessageWriter rpcMessageLocal = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.ReturnPiece, SendOption.Reliable, -1);
			rpcMessageLocal.Write(PlayerControl.LocalPlayer.PlayerId);
			AmongUsClient.Instance.FinishRpcImmediately(rpcMessageLocal);
			oldPlayer.gameObject.active = true;
			oldPlayer.name = oldPlayer.name[1..];
			Game.LocalActivity = EnumActivity.GameSelect;
		}

		public static int FindIndexById(int playerId)
		{
			int playerIndex = -1;
			for (int i = 0; i < Game.AllPlayers.Count; i++)
			{
				if (Game.AllPlayers[i].PlayerId == playerId) playerIndex = i;
			}
			return playerIndex;
		}
	}
}
