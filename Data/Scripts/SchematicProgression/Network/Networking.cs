using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;

using SchematicProgression.Settings;

using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace SchematicProgression.Network
{
  public class Networking
  {
    public readonly ushort ChannelId;
    private List<IMyPlayer> _tempPlayers;
    internal Session SessionComp;

    /// <summary>
    /// <paramref name="channelId"/> must be unique from all other mods that also use network packets.
    /// </summary>
    public Networking(ushort channelId, Session sessionComp)
    {
      ChannelId = channelId;
      SessionComp = sessionComp;
    }
    /// <summary>
    /// Register packet monitoring, not necessary if you don't want the local machine to handle incomming packets.
    /// </summary>
    public void Register()
    {
      MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ChannelId, ReceivedPacket);
    }

    /// <summary>
    /// This must be called on world unload if you called <see cref="Register"/>.
    /// </summary>
    public void Unregister()
    {
      MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ChannelId, ReceivedPacket);
    }

    private void ReceivedPacket(ushort handlerId, byte[] rawData, ulong senderId, bool fromServer) // executed when a packet is received on this machine
    {
      try
      {
        var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
        if (packet == null)
        {
          SessionComp?.Logger?.Log($"Networking.ReceivedPacket: Packet was null. IsServer = {SessionComp.IsServer}, IsDedi = {SessionComp.IsDedicatedServer}", MessageType.WARNING);
          return;
        }

        HandlePacket(packet, rawData);
      }
      catch (Exception e)
      {
        SessionComp?.Logger?.Log($"Error in Networking.ReceivedPacket:\n{e.Message}\n{e.StackTrace}", MessageType.ERROR);

        if (MyAPIGateway.Session?.Player != null)
          MyAPIGateway.Utilities.ShowNotification($"[ERROR: {GetType().FullName}: {e.Message} | Send log to mod author]", 10000, MyFontEnum.Red);
      }
    }

    private void HandlePacket(PacketBase packet, byte[] rawData = null)
    {
      var relay = packet.Received(this);

      if (relay)
        RelayToClients(packet, rawData);
    }

    /// <summary>
    /// Send a packet to the server.
    /// Works from clients and server.
    /// </summary>
    public void SendToServer(PacketBase packet)
    {
      if (MyAPIGateway.Multiplayer.IsServer)
      {
        HandlePacket(packet);
        return;
      }

      var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

      MyAPIGateway.Multiplayer.SendMessageToServer(ChannelId, bytes);
    }

    /// <summary>
    /// Send a packet to a specific player.
    /// Only works server side.
    /// </summary>
    public void SendToPlayer(PacketBase packet, ulong steamId)
    {
      if (!MyAPIGateway.Multiplayer.IsServer)
        return;

      var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

      MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, bytes, steamId);
    }

    /// <summary>
    /// Sends packet (or supplied bytes) to all players except server player and supplied packet's sender.
    /// Only works server side.
    /// </summary>
    public void RelayToClients(PacketBase packet, byte[] rawData = null)
    {
      if (!MyAPIGateway.Multiplayer.IsServer)
        return;

      if (_tempPlayers == null)
        _tempPlayers = new List<IMyPlayer>(MyAPIGateway.Session.SessionSettings.MaxPlayers);
      else
        _tempPlayers.Clear();

      if (rawData == null)
        rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);

      MyAPIGateway.Players.GetPlayers(_tempPlayers);

      foreach (var p in _tempPlayers)
      {
        if (p.IsBot)
          continue;

        if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
          continue;

        if (p.SteamUserId == packet.SenderId)
          continue;

        MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, rawData, p.SteamUserId);
      }

      _tempPlayers.Clear();
    }
  }
}
