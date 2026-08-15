using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace AmongChess.Rpc
{
	internal class Rpc
	{
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		public static class HandleRpcPatch
		{
			public static void Postfix(byte callId, MessageReader reader)
			{
				switch ((EnumRpc)callId)
				{
					case EnumRpc.MovePiece:
					{
						(byte x, byte y) fromCoordinates = (0, 0);
						(byte x, byte y) toCoordinates = (0, 0);
						fromCoordinates.x = reader.ReadByte();
						fromCoordinates.y = reader.ReadByte();
						toCoordinates.x = reader.ReadByte();
						toCoordinates.y = reader.ReadByte();
						Game.Utils.AddIncrementTime(Game.Game.PlayerTurn);
						Game.Game.GetAndPlayMove(fromCoordinates, toCoordinates);
						char[,] chessBoard = Chess.Chess.PlayMove(fromCoordinates, toCoordinates);
						Game.Utils.IncrementTurn();
						Chess.Chess.ChessBoard = chessBoard;
						break;
					}
					case EnumRpc.SelectPiece:
					{
						byte playerId = reader.ReadByte();
						byte selectedPiece = reader.ReadByte();
						PlayerControl playerControl = Game.Utils.FindPlayer(playerId);
						if (playerControl == null || playerControl.Data == null) break;
						playerControl.SetHat(Game.Utils.PieceHats[selectedPiece].ToString(), playerControl.Data.DefaultOutfit.ColorId);
						playerControl.SetSkin(Game.Utils.PieceSkins[selectedPiece].ToString(), playerControl.Data.DefaultOutfit.ColorId);
						playerControl.SetPet("");
						break;
					}
					case EnumRpc.ReturnPiece:
					{
						byte playerId = reader.ReadByte();
						Game.Utils.RevertClothingById(playerId);
						break;
					}
					case EnumRpc.GameResult:
					{
						byte winEvent = reader.ReadByte();
						byte winnerId = reader.ReadByte();
						Game.Game.EventEnded((Chess.EnumResults)winEvent, winnerId, false);
						break;
					}
					case EnumRpc.CustomOptions:
					{
						if (Lobby.Options.AllOption.Count == 0)
						{
							Lobby.Options.AllOption = Lobby.Options.OptionDefault();
							Lobby.Options.AllOptionGroup = Lobby.Options.OptionGroupDefault();
						}
						while (reader.BytesRemaining > 0)
						{
							byte optionId = reader.ReadByte();
							Lobby.ClassOption optionSingle = Lobby.Options.AllOption.Find(option => option.Id == optionId);
							optionSingle.Value = reader.ReadByte();
							Lobby.Options.NotifyOptionChanged(optionSingle);
						}
						break;
					}
					case EnumRpc.GameEnd:
					{
						if (AmongUsClient.Instance.AmHost)
						{
							GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByVote, false);
						}
						break;
					}
					case EnumRpc.SynchronizeTime:
					{
						float time = reader.ReadSingle();
						Game.Game.AllCustomPlayers[Game.Game.PlayerTurn].Timer = time;
						break;
					}
					case EnumRpc.PlayerLoaded:
					{
						if (AmongUsClient.Instance.AmHost)
						{
							byte playerId = reader.ReadByte();
							Game.CustomPlayer cp = Game.Utils.FindCustom(playerId);
							if (cp != null) cp.Loaded = true;
							Game.CustomPlayer local = Game.Utils.FindCustom(PlayerControl.LocalPlayer.PlayerId);
							if (local != null) local.Loaded = true;
							if (Game.Game.AllCustomPlayers.Count > 0 && Game.Game.AllCustomPlayers.TrueForAll(ele => ele.Loaded == true))
							{
								int[] colorIds = (int[])Game.Game.ColorIds.GetValue(Game.Game.AllPlayers.Count - 1);
								Game.Game.LocalActivity = PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId == colorIds[0] ? Game.EnumActivity.GameSelect : Game.EnumActivity.Lobby;
								MessageWriter rpcMessageTime = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)EnumRpc.GameStart, SendOption.Reliable, -1);
								AmongUsClient.Instance.FinishRpcImmediately(rpcMessageTime);
							}
						}
						break;
					}

					case EnumRpc.GameStart:
					{
						int[] colorIds = (int[])Game.Game.ColorIds.GetValue(Game.Game.AllPlayers.Count - 1);
						Game.Game.LocalActivity = PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId == colorIds[0] ? Game.EnumActivity.GameSelect : Game.EnumActivity.Lobby;
						break;
					}
				}
			}
		}

	}
}