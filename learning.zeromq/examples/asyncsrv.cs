//
//  Asynchronous client-to-server (DEALER to ROUTER)
//
//  While this example runs in a single process, that is just to make
//  it easier to start and stop the example. Each task has its own
//  context and conceptually acts as a separate process.

//  Author:     Michael Compton, Tomas Roos
//  Email:      michael.compton@littleedge.co.uk, ptomasroos@gmail.com

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ZeroMQ;
using zguide;

namespace zguide.asycnsrv
{
    internal class Program
    {
        private const int CLIENT_WORKERS = 3;
        private const int SERVER_WORKERS = 5;

        private const string SERVER_ADDRESS = "tcp://localhost:5570";
        private const string SERVER_FRONTEND_ADDRESS = "tcp://*:5570";
        private const string SERVER_BACKEND_ADDRESS = "inproc://backend";

        //  This main thread simply starts several clients, and a server, and then
        //  waits for the server to finish.
        public static void Main(string[] args)
        {
            var clients = new List<Thread>(3);
            for (int clientNumber = 0; clientNumber < CLIENT_WORKERS; clientNumber++)
            {
                clients.Add(new Thread(ClientTask));
                clients[clientNumber].Start();
            }

            var serverThread = new Thread(ServerTask);
            serverThread.Start();

            Console.ReadLine();
        }

        //  ---------------------------------------------------------------------
        //  This is our client task
        //  It connects to the server, and then sends a request once per second
        //  It collects responses as they arrive, and it prints them out. We will
        //  run several client tasks in parallel, each with a different random ID.
        public static void ClientTask()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            using (var context = ZmqContext.Create())
            {
                using (ZmqSocket client = context.CreateSocket(SocketType.DEALER))
                {
                    //  Generate printable identity for the client
                    string identity = ZHelpers.SetID(client, Encoding.Unicode);
                    client.Connect(SERVER_ADDRESS);

                    client.ReceiveReady += (s, e) =>
                    {
                        var zmsg = new ZMessage(e.Socket);
                        Console.WriteLine("{0} : {1}", identity, zmsg.BodyToString());
                    };

                    int requestNumber = 0;

                    var poller = new Poller(new List<ZmqSocket> { client });

                    while (true)
                    {
                        //  Tick once per second, pulling in arriving messages
                        for (int centitick = 0; centitick < 100; centitick++)
                        {
                            poller.Poll(TimeSpan.FromMilliseconds(10));
                        }
                        var zmsg = new ZMessage("");
                        zmsg.StringToBody(String.Format("thread: {0} --> request: {1}", threadId, ++requestNumber));
                        zmsg.Send(client);
                    }
                }
            }
        }

        //  ---------------------------------------------------------------------
        //  This is our server task
        //  It uses the multithreaded server model to deal requests out to a pool
        //  of workers and route replies back to clients. One worker can handle
        //  one request at a time but one client can talk to multiple workers at
        //  once.
        private static void ServerTask()
        {
            var workers = new List<Thread>(5);
            using (var context = ZmqContext.Create())
            {
                using (ZmqSocket frontend = context.CreateSocket(SocketType.ROUTER), backend = context.CreateSocket(SocketType.DEALER))
                {
                    frontend.Bind(SERVER_FRONTEND_ADDRESS);
                    backend.Bind(SERVER_BACKEND_ADDRESS);

                    for (int workerNumber = 0; workerNumber < SERVER_WORKERS; workerNumber++)
                    {
                        workers.Add(new Thread(ServerWorker));
                        workers[workerNumber].Start(context);
                    }

                    //  Switch messages between frontend and backend
                    frontend.ReceiveReady += (s, e) =>
                    {
                        var zmsg = new ZMessage(e.Socket);
                        zmsg.Send(backend);
                    };

                    backend.ReceiveReady += (s, e) =>
                    {
                        var zmsg = new ZMessage(e.Socket);
                        zmsg.Send(frontend);
                    };

                    var poller = new Poller(new List<ZmqSocket> {frontend, backend});

                    while (true)
                    {
                        poller.Poll();
                    }
                }
            }
        }

        //  Accept a request and reply with the same text a random number of
        //  times, with random delays between replies.
        private static void ServerWorker(object context)
        {
            var randomizer = new Random(DateTime.Now.Millisecond);
            using (ZmqSocket worker = ((ZmqContext)context).CreateSocket(SocketType.DEALER))
            {
                worker.Connect(SERVER_BACKEND_ADDRESS);

                while (true)
                {
                    //  The DEALER socket gives us the address envelope and message
                    var zmsg = new ZMessage(worker);
                    //  Send 0..4 replies back
                    int replies = randomizer.Next(5);
                    for (int reply = 0; reply < replies; reply++)
                    {
                        Thread.Sleep(randomizer.Next(1, 1000));
                        zmsg.Send(worker);
                    }
                }
            }
        }
    }
}
