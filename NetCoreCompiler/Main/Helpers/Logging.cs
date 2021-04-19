using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

namespace NetCoreCompiler
{
    public static class Logging
    {
        public static TcpListener tcpListener;
        public static List<Tuple<Socket, StreamWriter, StreamReader>> sockets = new List<Tuple<Socket, StreamWriter, StreamReader>>();
        public static void StartTCPListener()
        {
            if (Variables.TCP_IP == "")
                return;
            System.Net.IPAddress ipAddr = System.Net.IPAddress.Parse(Variables.TCP_IP);
            tcpListener = new TcpListener(ipAddr, 2055);
            tcpListener.Start();


            Console.WriteLine("TCP Listener started on " + Variables.TCP_IP + ":2055...");


            ThreadStart ts = new ThreadStart(() =>
            {
                while (true)
                {
                    try
                    {
                        if (tcpListener == null)
                            break;

                        Console.WriteLine("Waiting for TCP connection(s)...");
                        var socket = tcpListener.AcceptSocket();
                        if (socket != null && socket.SocketType == SocketType.Stream && socket.Connected)
                        {
                            Stream s = new NetworkStream(socket);
                            StreamWriter sw = new StreamWriter(s);
                            StreamReader sr = new StreamReader(s);
                            sw.AutoFlush = true;

                            var existingSock = sockets.Where(x => (x.Item1.RemoteEndPoint as IPEndPoint).Address.ToString() == (socket.RemoteEndPoint as IPEndPoint).Address.ToString()).FirstOrDefault();
                            if (existingSock != null)
                            {
                                bool stillConnect = true;
                                if (!existingSock.Item1.IsConnected())
                                {
                                    sockets.Remove(existingSock);
                                    stillConnect = false;
                                }

                                sockets.Add(new Tuple<Socket, StreamWriter, StreamReader>(socket, sw, sr));

                                if (stillConnect)
                                    TransmitLog("New TCP connection found from same client (" + (socket.RemoteEndPoint as IPEndPoint).Address + ")... Welcome!", null);
                                else
                                    TransmitLog("TCP connection with " + (socket.RemoteEndPoint as IPEndPoint).Address + " re-established... Welcome!", null);
                            }
                            else
                            {
                                sockets.Add(new Tuple<Socket, StreamWriter, StreamReader>(socket, sw, sr));

                                TransmitLog("TCP connection found (" + (socket.RemoteEndPoint as IPEndPoint).Address + ")... Welcome!", null);
                            }


                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });
            Thread thread = new Thread(ts);
            thread.Start();
        }

        public static void StopTCPListener()
        {
            sockets.ForEach(socket =>
            {
                if (socket.Item1 != null)
                    socket.Item1.Disconnect(false);
                if (socket.Item2 != null)
                    socket.Item2.Close();
                if (socket.Item3 != null)
                    socket.Item3.Close();
            });

            sockets.Clear();

            tcpListener.Stop();

            tcpListener = null;
        }
        public static void TransmitLog(string txt, string website)
        {
            if (website == null)
                website = "System";

            Console.WriteLine("Broadcasting: " + txt);
            // transmit to all connect clients
            sockets.ForEach(soc =>
            {
                if (soc != null && soc.Item1 != null && soc.Item1.SocketType == SocketType.Stream && soc.Item1.IsConnected())
                {
                    if (txt != null)
                        txt = txt.Replace("\r\n", "\r\n" + website + "|");
                    if (soc.Item2 != null)
                        soc.Item2.WriteLine(website + "|" + txt);
                }
            });


        }


    }
}
