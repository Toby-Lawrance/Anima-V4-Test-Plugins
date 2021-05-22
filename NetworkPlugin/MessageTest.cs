using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using Core;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Core.CoreData;
using Newtonsoft.Json;

namespace NetworkPlugin
{
    public static class MessageHolder
    {
        private static bool IPv6Support = Socket.OSSupportsIPv6;
        public static KnowledgeBase<int> ports = new KnowledgeBase<int>();
        public static KnowledgeBase<string[]> addresses = new KnowledgeBase<string[]>();
        public class MessageServer : Core.Plugins.Module
        {
            private StreamReader inStream;
            private TcpListener server;
            private bool SuccessfulSetup = false;

            public MessageServer() : base("MessageServer", "Receives messages from other servers", 1) { Enabled = false; }

            public override void Init()
            {
                base.Init();
                if (!IPv6Support)
                {
                    Anima.Instance.ErrorStream.WriteLine("IPv6 is not supported");
                    return;
                }

                if (server is null)
                {
                    if (ports.Exists("IP-Port"))
                    {
                        ports.TryGetValue("IP-Port", out int Port);
                        var info = Dns.GetHostEntry(Dns.GetHostName());
                        info.AddressList.Select(a => Anima.Instance.WriteLine($"Server Address: {a}"));
                        var address = info.AddressList[0];
                        var end = new IPEndPoint(address, Port);
                        server = new TcpListener(IPAddress.IPv6Any, Port);
                        server.Start();
                        this.StartTask(ListenAndRespond,TaskCreationOptions.LongRunning);
                        SuccessfulSetup = true;
                    }
                    else
                    {
                        addresses.TryInsertValue("IP-Addresses", new string[] { });
                        ports.TryInsertValue("IP-Port", 0);
                        Anima.Instance.ErrorStream.WriteLine("Error: Needed to create values in Anima pool");
                    }
                }
            }

            private void ListenAndRespond()
            {
                while (true)
                {
                    try
                    {
                        var client = server.AcceptTcpClient();

                        inStream = new StreamReader(client.GetStream());

                        string ReadContents = "";
                        string line = "";
                        while ((line = inStream.ReadLine()) != "<EOF>")
                        {
                            ReadContents += line + Anima.NewLineChar;
                        }
                        Anima.Instance.WriteLine($"Received: {ReadContents} from: {client.Client.RemoteEndPoint}");
                        Anima.Instance.SystemMail.PostMessage(new Message<string>(client.Client.RemoteEndPoint.ToString(), this.Identifier,
                            "Remote", ReadContents));
                        client.Close();
                    }
                    catch(SocketException se)
                    {
                        if (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                        {
                            Anima.Instance.ErrorStream.WriteLine(se.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        Anima.Instance.ErrorStream.WriteLine(e.Message);
                    }
                }
            }

            public override void Tick()
            {
                if (!SuccessfulSetup) return;
            }

            public override void Close()
            {
                base.Close();
                if (!SuccessfulSetup) return;
                server.Stop();
            }
        }

        public class MessageClient : Core.Plugins.Module
        {
            private Dictionary<IPAddress,int> outBoundClients;
            private int port;
            private bool SuccessfulSetup = false;
            private ManualResetEvent GetHostEntryFinished = new ManualResetEvent(false);

            private static readonly int MaxFailures = 10;

            public MessageClient() : base("MessageClient", "Sends messages to other computers", 2) { Enabled = false; }

            private async Task<(bool,IPAddress)> TryConnect(IPAddress a, int p)
            {
                try
                {
                    var tcp = new TcpClient(AddressFamily.InterNetworkV6);
                    await tcp.ConnectAsync(a,p);
                    return (true,a);
                }
                catch (Exception e)
                {
                    Anima.Instance.ErrorStream.WriteLine($"Unable to connect to:{a} because {e.Message}");
                    return (false,a);
                }
            }

            private TcpClient TryConnectClient(IPAddress a, int p)
            {
                try
                {
                    var tcp = new TcpClient(AddressFamily.InterNetworkV6);
                    tcp.Connect(a, p);
                    return tcp;
                }
                catch (Exception e)
                {
                    Anima.Instance.ErrorStream.WriteLine($"Unable to connect to:{a} because {e.Message}");
                    return null;
                }
            }

            public override void Init()
            {
                base.Init();
                if (!IPv6Support)
                {
                    Anima.Instance.ErrorStream.WriteLine("IPv6 is not supported");
                    return;
                }

                if (outBoundClients is not null) return;

                outBoundClients = new Dictionary<IPAddress, int>();
                if (addresses.Exists("IP-Addresses") && ports.Exists("IP-Port"))
                {
                    MessageHolder.addresses.TryGetValue("IP-Addresses", out string[] addresses);
                    ports.TryGetValue("IP-Port", out port);

                    var connectionTasks = addresses.Select(IPAddress.Parse).Select(ip => TryConnect(ip,port)).ToArray();

                    Task.WaitAll(connectionTasks);

                    foreach (var task in connectionTasks)
                    {
                        if (task.Result.Item1)
                        {
                            outBoundClients.Add(task.Result.Item2,1);
                        }
                        else
                        {
                            outBoundClients.Add(task.Result.Item2,0);
                        }
                    }
                    SuccessfulSetup = true;
                }
                else
                {
                    addresses.TryInsertValue("IP-Addresses", new string[] { });
                    ports.TryInsertValue("IP-Port", 0);
                    Anima.Instance.ErrorStream.WriteLine("Error: Needed to create IP-Addresses in Anima pool");
                }
            }

            private async Task<(bool,IPAddress)> TrySendMessage(IPAddress a, string message)
            {
                var tcp = TryConnectClient(a, port);
                if (tcp is null) return (false,a);

                var strem = new StreamWriter(tcp.GetStream());
                await strem.WriteLineAsync(message);
                await strem.FlushAsync();
                return (true,a);

            }

            public KnowledgeBase<int> counting = new KnowledgeBase<int>();

            public override void Tick()
            {
                if (!SuccessfulSetup) return;
                if (!counting.Exists("Count")) return;

                Anima.Instance.WriteLine($"Attempting to send network messages");

                var message = Anima.Serialize(new KeyValuePair<string, int>("Count", counting.Pool["Count"]));
                var tasks = outBoundClients.Where(tup => tup.Value > (MaxFailures * -1)).Select(tup => TrySendMessage(tup.Key,message)).ToArray();
                Task.WaitAll(tasks);
                foreach (var task in tasks)
                {
                    if (task.Result.Item1)
                    {
                        outBoundClients[task.Result.Item2]++;
                    }
                    else
                    {
                        outBoundClients[task.Result.Item2]--;
                    }
                }
            }

            public override void Close()
            {
                base.Close();
                if (!SuccessfulSetup) return;
            }
        }
    }

}
