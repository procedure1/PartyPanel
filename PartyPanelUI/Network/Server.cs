﻿using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PartyPanelShared;
using PartyPanelShared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PartyPanelUI.Network
{
    public class NetworkPlayer
    {
        public int id;
        public Socket workSocket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public List<byte> accumulatedBytes = new List<byte>();
    }

    public class Server
    {
        public static Server instance;
        public Action<NetworkPlayer, Packet> PacketRecieved;
        public Action<NetworkPlayer> PlayerConnected;
        public Action<NetworkPlayer> PlayerDisconnected;

		public static bool isConnected;

        public bool Enabled { get; set; } = true;

        private List<NetworkPlayer> players = new List<NetworkPlayer>();
        private Socket server;
        private int port;
        private Random rand = new Random();

        private static ManualResetEvent accpeting = new ManualResetEvent(false);

        public Server(int port)
        {
            this.port = port;
        }

        public void Start()
        {
            instance = this;
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            server = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(localEndPoint);
            server.Listen(100);
            Task.Run(() =>
            {
                while (Enabled)
                {
                    // Set the event to nonsignaled state.  
                    accpeting.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Logger.Debug("Waiting for a connection...");
                    server.BeginAccept(new AsyncCallback(AcceptCallback), server);

                    // Wait until a connection is made before continuing.  
                    accpeting.WaitOne();
                }
            }).ConfigureAwait(false);
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            accpeting.Set();

            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                NetworkPlayer player = new NetworkPlayer();
                player.id = rand.Next(int.MaxValue);
                player.workSocket = handler;

                lock (players)
                {
                    players.Add(player);
                }

                PlayerConnected?.Invoke(player);

                handler.BeginReceive(player.buffer, 0, NetworkPlayer.BufferSize, 0, new AsyncCallback(ReadCallback), player);
				isConnected = true;
				Logger.Info("Connected");
			}
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            NetworkPlayer player = (NetworkPlayer)ar.AsyncState;
            try
            {
                Socket handler = player.workSocket;
                NetworkStream streama = new NetworkStream(handler);
                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)
                {
                    var currentBytes = new byte[bytesRead];
                    Buffer.BlockCopy(player.buffer, 0, currentBytes, 0, bytesRead);

                    player.accumulatedBytes.AddRange(currentBytes);
                    if (player.accumulatedBytes.Count >= Packet.packetHeaderSize)
                    {
                        //If we're not at the start of a packet, increment our position until we are, or we run out of bytes
                        var accumulatedBytes = player.accumulatedBytes.ToArray();
                        while (!Packet.StreamIsAtPacket(accumulatedBytes) && accumulatedBytes.Length >= Packet.packetHeaderSize)
                        {
                            player.accumulatedBytes.RemoveAt(0);
                            accumulatedBytes = player.accumulatedBytes.ToArray();
                        }
                        if (Packet.PotentiallyValidPacket(accumulatedBytes))
                        {
                            try
                            {
                                PartyPanelUI.Server.Server_PacketRecieved(player, Packet.FromBytes(accumulatedBytes));
                            }
                            catch (Exception e)
                            {
                                //Logger.Debug(e.ToString());
                            }
                            player.accumulatedBytes.Clear();
                        }
                    }
                    // Not all data received. Get more.  
                    handler.BeginReceive(player.buffer, 0, NetworkPlayer.BufferSize, 0, new AsyncCallback(ReadCallback), player);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                PlayerDisconnected_Internal(player);
            }
        }

        private void PlayerDisconnected_Internal(NetworkPlayer player)
        {
            lock (players)
            {
                players.Remove(player);
            }
            PlayerDisconnected?.Invoke(player);
        }

        public void Send(byte[] data, string test = "empty")
        {
            NetworkPlayer player = null;
            lock (players)
            {
                //TODO: Potentially could add multi-client support here, but there's really no need at the present time
                //player = players.First(x => x.id == playerId);
                player = players.LastOrDefault();
            }

            try
            {
                //Get the socket for the specified playerId
                var socket = player?.workSocket;

				// Begin sending the data to the remote device.
				socket?.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), player);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                PlayerDisconnected_Internal(player);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            NetworkPlayer player = (NetworkPlayer)ar.AsyncState;

            try
            {
                // Retrieve the socket from the state object.  
                var handler = player.workSocket;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
                PlayerDisconnected_Internal(player);
            }
        }

        public void Shutdown()
        {
            Enabled = false;
            if (server.Connected) server.Shutdown(SocketShutdown.Both);
            server.Close();
        }
    }
}