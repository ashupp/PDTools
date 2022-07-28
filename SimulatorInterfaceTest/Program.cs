using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using PDTools.SimulatorInterface;

namespace SimulatorInterfaceTest
{
    internal class Program
    {
        private static bool _showUnknown = false;
        private static bool _udpSend = false;
        private static UdpClient _udpClient;
        private static IPEndPoint _ipEndPoint;

        static async Task Main(string[] args)
        {
            /* Mostly a test sample for using the Simulator Interface library */

            Console.WriteLine("Simulator Interface GT7/GTSport - Nenkai#9075");
            Console.WriteLine();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: SimulatorInterface.exe <IP address of PS4/PS5> ('--gtsport' for GT Sport support, optional: '--debug' to show unknown values)");
                Console.WriteLine("--udpsend to send udp data to local port 26998 for simfeedback usage");
                return;
            }

            _showUnknown = args.Contains("--debug");
            bool gtsport = args.Contains("--gtsport");
            _udpSend = args.Contains("--udpsend");

            Console.WriteLine("Starting interface..");

            SimulatorInterface simInterface = new SimulatorInterface(args[0], !gtsport ? SimulatorInterfaceGameType.GT7 : SimulatorInterfaceGameType.GTSport);
            simInterface.OnReceive += SimInterface_OnReceive;

            var cts = new CancellationTokenSource();

            // Cancel token from outside source to end simulator

            var task = simInterface.Start(cts);

            if (_udpSend)
            {
                _ipEndPoint = IPEndPoint.Parse("127.0.0.1");
                _ipEndPoint.Port = 26998;
                _udpClient = new UdpClient();
                _udpClient.ExclusiveAddressUse = false;
            }


            try
            {
                await task;
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"Simulator Interface ending..");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Errored during simulation: {e.Message}");
            }
            finally
            {
                // Important to clear up underlaying socket
                simInterface.Dispose();
            }
        }


        private static double LoopAngle(double angle, double minMag)
        {
            double absAngle = Math.Abs(angle);

            if (absAngle <= minMag)
            {
                return angle;
            }

            double direction = angle / absAngle;

            double loopedAngle = (180.0f * direction) - angle;

            return loopedAngle;
        }

        private static void SimInterface_OnReceive(SimulatorPacketBase packet)
        {
            Console.SetCursorPosition(0, 0);
            packet.PrintPacket(_showUnknown);

            if (_udpSend)
            {
                var tmpPacket = packet as SimulatorPacketG7S0;

                try
                {
                    var sendString = "pitch=" + (Math.Asin(tmpPacket.Rotation.X) * 360 / Math.PI).ToString("F5") + Environment.NewLine + 				// ok 
                                     "roll=" + LoopAngle(Math.Asin(tmpPacket.Rotation.Z) * 360 / Math.PI, 180).ToString("F5") + Environment.NewLine +  	// ok
                                     "surge=" + (tmpPacket.Velocity.X * 0.10197162129779).ToString("F5") + Environment.NewLine + 						// test
                                     "yaw=" + tmpPacket.Velocity.Z.ToString("F5") + Environment.NewLine +   											// test
                                     "heave=" + (tmpPacket.Velocity.Y * 0.10197162129779).ToString("F5") + Environment.NewLine +						// ok
                                     "rpm=" + tmpPacket.EngineRPM.ToString("F0") + Environment.NewLine +												// ok
                                     "speed=" + (tmpPacket.MetersPerSecond * 3.6).ToString("F0") + Environment.NewLine;									// ok

                    Byte[] sendBytes = Encoding.ASCII.GetBytes(sendString);
                    Console.WriteLine("Data sent : " + sendString);
                    _udpClient.Send(sendBytes, sendBytes.Length, _ipEndPoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending Data to simfeedback");
                }

            }
        }
    }
}