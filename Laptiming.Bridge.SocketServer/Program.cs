using System;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

namespace Laptiming.Bridge.SocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new TcpListener(IPAddress.Loopback, 11000);
            server.Start();
            var incoming = server.AcceptTcpClient();

            while (true)
            {
                try
                {
                    var networkStream = incoming.GetStream();
                    var bytesFrom = new byte[65536]; // 2^16
                    networkStream.Read(bytesFrom, 0, incoming.ReceiveBufferSize);
                    var dataFromClient = Encoding.ASCII.GetString(bytesFrom);
                    dataFromClient = dataFromClient.Substring(0, Math.Max(dataFromClient.IndexOf("$"), 0));
                    Console.WriteLine(" IN: " + dataFromClient);

                    var messageFunction = dataFromClient.Split('@').Skip(1).First();
                    var ackMessage = "";
                    if (messageFunction == "Store")
                    {
                        var msg = ParseStoreMessage(dataFromClient);

                        ackMessage = msg.MessageNumber.ToString();
                    }

                    var serverResponse = $"Server@Ack{messageFunction}@{ackMessage}$";
                    var sendBytes = Encoding.ASCII.GetBytes(serverResponse);
                    networkStream.Write(sendBytes, 0, sendBytes.Length);
                    networkStream.Flush();
                    Console.WriteLine("OUT: " + serverResponse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private static StoreMessage ParseStoreMessage(string dataFromClient)
        {
            var parts = dataFromClient.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
            var i = 0;
            var storeMessage = new StoreMessage();
            foreach (var part in parts)
            {
                if (i == 0)
                {
                    storeMessage.Location = part;
                }
                else if (i == 1)
                {
                    i++;
                    continue; // part == "Store" (function name)
                }
                else
                {
                    if (int.TryParse(part, out int messageNumber)) // last part, containing count
                    {
                        storeMessage.MessageNumber = messageNumber;
                    }
                    else // actual passing
                    {
                        storeMessage.Passings.Add(Passing.Parse(part));
                    }
                }
                i++;
            }

            return storeMessage;
        }

        /// <summary>
        /// Instruction to store passings. Max 50 per message
        /// </summary>
        public class StoreMessage
        {
            public StoreMessage()
            {
                Passings = new List<Passing>();
            }
            public string Location { get; set; }
            public List<Passing> Passings { get; set; }
            public int MessageNumber { get; set; }
        }

        public class Passing
        {
            /// <summary>
            /// 7 chars Chip code
            /// </summary>
            public string Code { get; set; }
            /// <summary>
            /// 12 chars Time, started with a space if the precision is in hundredth of a second.
            /// So “ 11:03:40.34” or “11:03:40.347”
            /// </summary>
            public string TimeLiteral { get; set; }
            /// <summary>
            /// 2 chars Device number
            /// </summary>
            public string DeviceNumber { get; set; }
            /// <summary>
            /// 2 chars Reader number
            /// </summary>
            public string ReaderNumber { get; set; }
            /// <summary>
            /// 1 hex char Antenna (LF), Detections counter clips on 16 (chipX)
            /// </summary>
            public char Antenna { get; set; }
            /// <summary>
            /// 3 chars Lap counter
            /// </summary>
            public string LapCounter { get; set; }
            /// <summary>
            /// 4 hex chars Sequence number (not used by all device types)
            /// </summary>
            public string SequenceNumber { get; set; }
            /// <summary>
            /// 6 chars Date in yyMMdd
            /// </summary>
            public string DateLiteral { get; set; }
            /// <summary>
            /// 2 chars Checksum
            /// </summary>
            public string Checksum { get; set; }

            public static Passing Parse(string part)
            {
                return new Passing
                {
                    Code = part.Substring(0, 7), // 7
                    TimeLiteral = part.Substring(7, 12), // 19
                    DeviceNumber = part.Substring(19, 2), // 21
                    ReaderNumber = part.Substring(21, 2), // 23
                    Antenna = part.Substring(23, 1).Single(), // 24
                    LapCounter = part.Substring(24, 3), // 27
                    SequenceNumber = part.Substring(27, 4), // 31
                    DateLiteral = part.Substring(31, 6), // 37
                    Checksum = part.Substring(37, 2), // 39
                };
            }
        }
    }
}