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
                        Console.WriteLine("Waiting for TCP connection...");
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
                                if (!existingSock.Item1.IsConnected())
                                {
                                    sockets.Remove(existingSock);
                                    Console.WriteLine("TCP connection with " + (socket.RemoteEndPoint as IPEndPoint).Address + " re-established...");
                                }
                                else
                                {
                                    Console.WriteLine("Another TCP connection found from same client (" + (socket.RemoteEndPoint as IPEndPoint).Address + ")...");
                                }
                            } else
                            {
                                Console.WriteLine("TCP connection found (" + (socket.RemoteEndPoint as IPEndPoint).Address + ")...");
                            }

                            sockets.Add(new Tuple<Socket, StreamWriter, StreamReader>(socket, sw, sr));

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
        }
        public static void TransmitLog(string txt)
        {
            // transmit to all connect clients
            sockets.ForEach(soc =>
            {
                if (soc != null && soc.Item1 != null && soc.Item1.SocketType == SocketType.Stream && soc.Item1.IsConnected())
                {
                    if (soc.Item2 != null)
                        soc.Item2.WriteLine(txt);
                }
            });


        }


    }
}
