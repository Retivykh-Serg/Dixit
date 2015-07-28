using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Globalization;
using System.IO;

namespace DixitServer
{
    enum Command
    {
        Connect,     
        Disconnect,     
        List,
        ListWaitingUsers,
        ListUsers,
        newGame,
        connectToGame,
        startGame,

        LeaderTurn,
        Waiting,
        GamersTurn,
        VoatingTurn,
        Result,

        Success,
        Failed,
        logOut,
        win,
        chat,
        setAdmin,
        OldVersion,
        Null
    }

    class ClientInfo
    {
        public Socket socket;   //идентификатор подключения
        public string login  {get; set;}  //имя пользователя
        public int roomId  {get; set;}   //номер комнаты, к которой подключен. 0-ни к какой
        public int discarded; //какую карту сбросил
        public int voated; //за какую карту голосовал
        public int scores { get; set; }
        public string ip { get; set; }
        public bool isLeader { get; set; }
        public bool isAdmin { get; set; }
        public ManualResetEvent acceptEvent = new ManualResetEvent(false);

        public string IsLeader
        {
            get
            {
                if (isLeader == true)
                    return "Да";
                return "";
            }
            set
            {
                isLeader = false;
                if (value == "Да")
                    isLeader = true;

            }
        }
        public string IsAdmin
        {
            get
            {
                if (isAdmin == true)
                    return "Да";
                return "";
            }
            set
            {
                isAdmin = false;
                if (value == "Да")
                    isLeader = true;

            }
        }
    }

    class Room
    {
        public int id { get; set; }
        public string roomName { get; set; }
        public int status { get; set; }
        public int maxScores { get; set; }
        public string gamers { get; set; }
        public int deckSize { get; set; }
        public string gamersSend;
        public string password { get; set; }
        public int phase;//0 ждем начало - 1 игра началась - 2 LeaderTurn - 3 clientWaiting 
        // 4 GamerTurn - 5 VoatingTurn 6 - result 8 - win
        public List<ClientInfo> clientsInRoom;
        public List<int> Cards;
        public int sendCount;
        public ManualResetEvent acceptAllEvent = new ManualResetEvent(false);

        public string Status
        {
            get
            {
                if (status == 0)
                    return "Ожидание";
                return "Игра";
            }
            set
            {
                status = 1;
                if (value == "Ожидание")
                    status = 0;

            }
        }
    }

    class PacketInfo
    {
        public Socket socket;
        public Command cmd;
        public int counter;

        public PacketInfo(Socket _socket, Command _cmd, int _counter)
        {
            cmd = _cmd;
            counter = _counter;
            socket = _socket;
        }
    }

    class Server
    {
        #region fields

        private int VERSION = 1005;
        public string log = "";
        public string log2 = "";

        Socket serverSocket;
        byte[] byteData = new byte[1024];

        public List<ClientInfo> clientList;
        public List<Room> rooms;

        public event EventHandler logChanged;
        public event EventHandler log2Changed;
        public event EventHandler playersChanged; //sender true - есть изменения для главного лобби
        public event EventHandler roomsChanged;

        private int logSize = 1000;
        private int currentLog1 = 0;
        private int currentLog2 = 0;

        private object globalLocker = new object();
        private object removeLobbyLocker = new object();
        private object lobbyLocker = new object();
        private ManualResetEvent acceptLobbyEvent = new ManualResetEvent(false);
        private int sendLobby = 0;

        public bool saveLog1 = true;
        public bool saveLog2 = true;

        private DispatcherTimer timer;

        #endregion
        
        #region public methods
        public Server()
        {
            clientList = new List<ClientInfo>();
            rooms = new List<Room>();
            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 1, 0);
            timer.Tick += new EventHandler(timer_Tick);
            timer.Start();

            try
            {
                //We are using TCP sockets
                serverSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);

                //Assign the any IP of the machine and listen on port number 1000
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1312);

                //Bind and listen on the given address
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(4);

                //Accept the incoming clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
                log = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server exeption on initialization");
            }
        }

        
        #endregion
        
        #region private methods
        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = serverSocket.EndAccept(ar);

                //Start listening for more clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server accept exeption");
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndReceive(ar);

                //Transform the array of bytes received from the user into an
                //intelligent form of object Data
                Data msgReceived = new Data(byteData);

                //We will send this object in response the users request
                Data msgToSend = new Data();

                lock (globalLocker)
                {
                    int clientId = findClient(clientList, clientSocket);
                    byte[] message;

                    if (clientId == -1) writeToLog2("Получено " + msgReceived.cmdCommand.ToString() + " от " + clientSocket.LocalEndPoint.ToString(), true);
                    else if (clientList[clientId].roomId == -1) writeToLog2("Получено " + msgReceived.cmdCommand.ToString() + " от "
                         + clientList[clientId].login, true);

                    else writeToLog2("Получено " + msgReceived.cmdCommand.ToString() + "\t от " + clientList[clientId].login + " в \""
                        + rooms[clientList[clientId].roomId].roomName + "\"", true);

                    switch (msgReceived.cmdCommand)
                    {
                        case Command.Connect:
                            /////////проверка разрешения пользователя на доступ к игре
                            //если разрешено

                            ClientInfo clientInfo = new ClientInfo();
                            clientInfo.socket = clientSocket;
                            clientInfo.ip = clientInfo.socket.RemoteEndPoint.ToString();
                            clientInfo.login = msgReceived.login;
                            clientInfo.roomId = -1;

                            if (msgReceived.cardID != VERSION)
                            {
                                writeToLog("Попытка подключиться (старый клиент): " + clientInfo.login);
                                msgToSend.cmdCommand = Command.OldVersion;
                                message = msgToSend.ToByte();
                                //writeToLog2("Отправлено к " + clientInfo.login + " " + msgToSend.cmdCommand.ToString(), true);
                                clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendOne),
                                    new PacketInfo(clientInfo.socket, Command.OldVersion, -1));
                                return;
                            }

                            if (isLoginOK(clientList, msgReceived.login))
                            {
                                writeToLog("Подключился новый игрок: " + clientInfo.login);
                                clientList.Add(clientInfo);

                                msgToSend.cmdCommand = Command.Success;
                                message = msgToSend.ToByte();
                                //writeToLog2("Отправлено к " + clientInfo.login + " " + msgToSend.cmdCommand.ToString(), true);
                                clientInfo.acceptEvent.Reset();
                                clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendOne),
                                    new PacketInfo(clientInfo.socket, Command.Success, -1));
                                clientInfo.acceptEvent.WaitOne();
                                if (playersChanged != null)
                                    playersChanged(false, null);
                            }
                            else
                            {
                                writeToLog("Попытка подключиться: " + clientInfo.login);
                                msgToSend.cmdCommand = Command.Failed;
                                message = msgToSend.ToByte();
                                //writeToLog2("Отправлено к " + clientInfo.login + " " + msgToSend.cmdCommand.ToString(), true);
                                clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendOne),
                                    new PacketInfo(clientInfo.socket, Command.Failed, -1));
                            }
                            break;

                        case Command.connectToGame:
                            //найти и добавить в игру клиента
                            if (clientId != -1)
                            {
                                clientList[clientId].roomId = findRoom(msgReceived.gameToConnectRoomName);
                                rooms[clientList[clientId].roomId].gamersSend += clientList[clientId].login + " ";
                                rooms[clientList[clientId].roomId].gamers += "; " + clientList[clientId].login;
                                rooms[clientList[clientId].roomId].clientsInRoom.Add(clientList[clientId]);

                                msgToSend.UsersInRoom = rooms[clientList[clientId].roomId].gamersSend;
                                Send4All(clientId, Command.ListUsers, msgToSend);

                                if (roomsChanged != null)
                                    roomsChanged(null, null);
                                playersChanged(true, null);

                                writeToLog(clientList[clientId].login + " присоединился в комнату " + rooms[clientList[clientId].roomId].roomName);
                            }
                            break;

                        case Command.newGame:
                            //создать новый стол
                            if (clientId != -1 && clientList[clientId].roomId == -1)
                            {
                                Room r = new Room();
                                r.phase = 0;
                                r.id = rooms.Count;
                                r.roomName = msgReceived.gameToConnectRoomName;
                                r.maxScores = msgReceived.cardID;
                                r.gamers = clientList[clientId].login;
                                r.password = msgReceived.login;
                                r.gamersSend = r.gamers + " ";
                                r.clientsInRoom = new List<ClientInfo>();
                                r.clientsInRoom.Add(clientList[clientId]);
                                r.deckSize = Convert.ToInt32(msgReceived.UsersInRoom);
                                r.Cards = new List<int>();
                                for (int i = 0; i < r.deckSize; i++)
                                    r.Cards.Add(i + 1);
                                clientList[clientId].isAdmin = true; //первый создавший - админ, может запустить игру
                                rooms.Add(r);
                                clientList[clientId].roomId = r.id;
                                writeToLog(clientList[clientId].login + " создал комнату: " + (r.roomName));

                                if (roomsChanged != null)
                                    roomsChanged(null, null);
                                playersChanged(true, null);
                            }
                            break;

                        #region GameLogic

                        case Command.startGame:
                            if (clientId != -1 && clientList[clientId].roomId != -1)
                            {
                                var r = rooms[clientList[clientId].roomId]; //активная комната
                                r.status = 1; //идет игра
                                r.phase = 1;
                                if (roomsChanged != null)
                                    roomsChanged(null, null);
                                r.gamersSend = r.gamersSend.Replace("# ", " ");
                                r.gamersSend = r.gamersSend.Replace("? ", " ");
                                r.gamersSend = r.gamersSend.Replace("* ", " ");
                                writeToLog("В комнате " + r.roomName + " началась игра!");
                                //msgToSend.UsersInRoom = r.gamersSend;
                                //Send4All(clientId, Command.ListUsers, msgToSend);
                                //начинаем игру раздаем карты
                                Random rnd = new Random();

                                r.sendCount = 0;
                                r.acceptAllEvent.Reset();
                                for (int i = 0; i < r.clientsInRoom.Count; i++)
                                {
                                    r.clientsInRoom[i].scores = 0; //очки в текущей партии
                                    r.clientsInRoom[i].voated = 0;
                                    r.clientsInRoom[i].discarded = 0;
                                    for (int j = 0; j < 5; j++)
                                        msgToSend.userCards[j] = NextCard(rnd, clientId);
                                    msgToSend.cardID = r.maxScores;
                                    msgToSend.cmdCommand = Command.startGame;
                                    message = msgToSend.ToByte();
                                    if (r.clientsInRoom[i].socket.Connected)
                                        r.clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendRoom),
                                            new PacketInfo(r.clientsInRoom[i].socket, Command.startGame, -2));
                                    else connectionLost("Socket disconnected", r.clientsInRoom[i].socket);
                                    //r.clientsInRoom[i].acceptEvent.WaitOne();
                                    //writeToLog2("Отправлено к " + r.clientsInRoom[i].login + " " + msgToSend.cmdCommand.ToString());
                                }

                                r.acceptAllEvent.WaitOne();

                                roundBegin(r, clientId, rnd, true);

                            }
                            break;

                        case Command.LeaderTurn:
                            if (clientId != -1 && clientList[clientId].roomId != -1)
                            {
                                var r = rooms[clientList[clientId].roomId]; //активная комната
                                int clientRoomId = findClient(r.clientsInRoom, clientSocket);

                                r.clientsInRoom[clientRoomId].discarded = msgReceived.cardID; //занесли в память что скинул
                                msgToSend.cmdCommand = Command.GamersTurn; //передает словесное задание
                                msgToSend.gameToConnectRoomName = msgReceived.gameToConnectRoomName;
                                message = msgToSend.ToByte();

                                if (r.clientsInRoom.Count > 1)
                                {
                                    r.acceptAllEvent.Reset();
                                    r.sendCount = 1;
                                    for (int i = 0; i < r.clientsInRoom.Count; i++)
                                    {
                                        if (i == findLeaderID(clientId)) continue;
                                        if (r.clientsInRoom[i].socket.Connected)
                                            r.clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                                new AsyncCallback(OnSendRoom), new PacketInfo(r.clientsInRoom[i].socket, Command.GamersTurn, -2));
                                        else connectionLost("Socket disconnected", r.clientsInRoom[i].socket);
                                        //writeToLog2("Отправлено к " + r.clientsInRoom[i].login + " " + msgToSend.cmdCommand.ToString());
                                    }

                                    r.acceptAllEvent.WaitOne();
                                }
                                //отчет в список
                                r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "*");
                                msgToSend.UsersInRoom = rooms[clientList[clientId].roomId].gamersSend;
                                Send4All(clientId, Command.ListUsers, msgToSend);
                                r.phase = 4; //ассоциация пришла - отправили команду игрокам для сброса карт
                            }
                            break;

                        case Command.GamersTurn:
                            if (clientId != -1 && clientList[clientId].roomId != -1)
                            {
                                var r = rooms[clientList[clientId].roomId]; //активная комната
                                int clientRoomId = findClient(r.clientsInRoom, clientSocket);
                                r.clientsInRoom[clientRoomId].discarded = msgReceived.cardID; //занесли в память что скинул
                                r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "#");
                                bool isReady = true;
                                foreach (ClientInfo cl in r.clientsInRoom)
                                    if (cl.discarded <= 0) { isReady = false; break; }
                                if (isReady)
                                {
                                    Data msgToSendV = new Data(rooms[clientList[clientId].roomId].clientsInRoom.Count);
                                    msgToSendV.cmdCommand = Command.VoatingTurn;
                                    msgToSendV.userCards = ShuffleDiscarded(clientId);
                                    Send4All(clientId, Command.VoatingTurn, msgToSendV);
                                    r.phase = 5; //все сбросили карты - время для голосования!
                                    r.gamersSend = rooms[clientList[clientId].roomId].gamersSend.Replace("# ", " ");
                                }
                                msgToSend.UsersInRoom = rooms[clientList[clientId].roomId].gamersSend;
                                Send4All(clientId, Command.ListUsers, msgToSend);
                            }
                            break;

                        case Command.VoatingTurn:
                            if (clientId != -1 && clientList[clientId].roomId != -1)
                            {
                                var r = rooms[clientList[clientId].roomId]; //активная комната
                                int clientRoomId = findClient(r.clientsInRoom, clientSocket);
                                r.clientsInRoom[clientRoomId].voated = msgReceived.cardID; //занесли в память что ответил

                                r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "#");
                                bool isReady = true;
                                foreach (ClientInfo cl in r.clientsInRoom)
                                {
                                    if (cl.login == r.clientsInRoom[findLeaderID(clientId)].login) continue;
                                    if (cl.voated <= 0 && cl.ip != "LoggedOut") { isReady = false; break; }
                                }

                                if (isReady) calcResult(r, clientId, msgToSend);

                                msgToSend.UsersInRoom = rooms[clientList[clientId].roomId].gamersSend;
                                Send4All(clientId, Command.ListUsers, msgToSend);

                                r.gamersSend = r.gamersSend.Replace("& ", " ");
                            }
                            break;

                        case Command.Success:
                            if (clientId != -1 && clientList[clientId].roomId != -1)
                            {
                                var r = rooms[clientList[clientId].roomId]; //активная комната
                                int clientRoomId = findClient(r.clientsInRoom, clientSocket);
                                r.clientsInRoom[clientRoomId].voated = 0; //сбрасываем сыгранные карты
                                r.clientsInRoom[clientRoomId].discarded = 0;
                                r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "#");

                                bool isReady = true;
                                foreach (ClientInfo cl in r.clientsInRoom)
                                    if (cl.discarded > 0) { isReady = false; break; }
                                if (isReady) roundBegin(r, clientId, new Random(), false);
                                else
                                {
                                    msgToSend.UsersInRoom = r.gamersSend;
                                    Send4All(clientId, Command.ListUsers, msgToSend);
                                }
                            }
                            break;

                        #endregion

                        case Command.List:
                            //сформировать список столов и вернуть назад
                            msgToSend.cmdCommand = Command.List;
                            msgToSend.list = rooms;
                            message = msgToSend.ToByte();

                            //Send the message to all users

                            if (clientSocket.Connected)
                                clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendOne),
                                    new PacketInfo(clientSocket, Command.List, -1));
                            else connectionLost("Socket disconnected", clientSocket);
                            //writeToLog2("Отправлено к " + clientList[clientId].login + " " + msgToSend.cmdCommand.ToString());
                            playersChanged(true, null);
                            break;

                        case Command.chat:
                            if (clientId != -1)
                            {
                                if (msgReceived.cardID == 1) //сообщение пришло для игровой комнаты
                                {
                                    if (msgReceived.UsersInRoom.StartsWith("@kick")) //получена команда
                                        if (clientList[clientId].isAdmin)
                                        {
                                            string target = msgReceived.UsersInRoom.Remove(0, 6);
                                            for (int i=0; i< rooms[clientList[clientId].roomId].clientsInRoom.Count; i++)
                                                if (rooms[clientList[clientId].roomId].clientsInRoom[i].login == target)
                                                {
                                                    msgToSend.cmdCommand = Command.Disconnect;
                                                    message = msgToSend.ToByte();
                                                    rooms[clientList[clientId].roomId].clientsInRoom[i].acceptEvent.Reset();
                                                    rooms[clientList[clientId].roomId].clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                                        new AsyncCallback(OnSendOne), new PacketInfo(rooms[clientList[clientId].roomId].clientsInRoom[i].socket, Command.Disconnect, -1));
                                                    rooms[clientList[clientId].roomId].clientsInRoom[i].acceptEvent.WaitOne();
                                                }
                                        }
                                        
                                        msgToSend.gameToConnectRoomName = msgReceived.UsersInRoom;
                                        msgToSend.login = clientList[clientId].login;
                                        msgToSend.cardID = 1;
                                        Send4All(clientId, Command.chat, msgToSend);
                                    
                                }
                                else
                                {
                                    msgToSend.gameToConnectRoomName = msgReceived.UsersInRoom;
                                    msgToSend.login = clientList[clientId].login;
                                    msgToSend.cmdCommand = Command.chat;
                                    msgToSend.cardID = 0;
                                    message = msgToSend.ToByte();
                                    lock (removeLobbyLocker)
                                    {
                                        acceptLobbyEvent.Reset();
                                        sendLobby = 0;
                                        int waiterNum = 0;
                                        for (int i = 0; i < clientList.Count; i++)
                                            if (clientList[i].roomId == -1)
                                                waiterNum++;
                                        if (waiterNum == 0) return;
                                        for (int i = 0; i < clientList.Count; i++)
                                            if (clientList[i].roomId == -1)
                                                if (clientList[i].socket.Connected)
                                                    clientList[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendLobby),
                                                     new PacketInfo(clientList[i].socket, Command.chat, waiterNum));
                                                else connectionLost("Socket disconnected", clientList[i].socket);
                                        acceptLobbyEvent.WaitOne();
                                    }

                                }
                            }
                            break;

                        case Command.logOut:
                            if (clientId != -1)
                            {
                                writeToLog("Игрок " + clientList[clientId].login + " вышел из комнаты " + rooms[clientList[clientId].roomId].roomName);
                                afterDisconnect(clientId, clientSocket, msgToSend);

                                clientList[clientId].roomId = -1;
                                roomsChanged(null, null);
                                playersChanged(true, null);
                            }
                            break;

                        case Command.Disconnect:
                            if (clientId != -1)
                            {
                                if (clientList[clientId].roomId != -1)
                                    afterDisconnect(clientId, clientSocket, new Data());

                                writeToLog("Игрок " + clientList[clientId].login + " отключился");
                                lock (removeLobbyLocker) clientList.RemoveAt(clientId);
                                roomsChanged(null, null);
                                playersChanged(true, null);
                            }
                            break;
                    }

                    //If the user is logging out then we need not listen from her
                    if (msgReceived.cmdCommand != Command.Disconnect && msgToSend.cmdCommand != Command.Failed)
                    {
                        //Start listening to the message send by the user
                        clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                    }
                }
                log2Changed(log, null);
            }

            catch (Exception ex)
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                connectionLost(ex.Message, clientSocket);
            }
        }

        private void connectionLost(string errorMessage, Socket clientSocket)
        {
            
            int clientId = findClient(clientList, clientSocket);
            if (clientId != -1)
            {
                writeToLog("Отключился игрок c ошибкой: " + clientList[clientId].login + "\n\t" + errorMessage);
                if (clientList[clientId].roomId != -1)
                {
                    afterDisconnect(clientId, clientSocket, new Data());
                    clientList[clientId].roomId = -1;
                }

                if (clientSocket.Connected)
                {
                    Data msgToSend = new Data();
                    msgToSend.cmdCommand = Command.Null;
                    byte[] message = msgToSend.ToByte();
                    //отправляем ему что ошибка вышла

                    clientList[clientId].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                            new AsyncCallback(OnSendOne), new PacketInfo(clientList[clientId].socket, Command.Null, -1));
                    //writeToLog2("Отправлено к " + clientList[clientId].login + " " + msgToSend.cmdCommand.ToString());
                }

                lock (removeLobbyLocker) clientList.RemoveAt(clientId);
                    roomsChanged(null, null);
                    playersChanged(true, null);
            }
            else
                MessageBox.Show(errorMessage, "Server receive exeption");
        }

        private void afterDisconnect(int clientId, Socket clientSocket, Data msgToSend)
        {
            bool IsNotRemoved = true;
            var r = rooms[clientList[clientId].roomId];
            int clientRoomId = findClient(r.clientsInRoom, clientSocket);
            if (clientList[clientId].isAdmin == true) //вышел админ, меняем его чтобы могли запускать игру
            {
                clientList[clientId].isAdmin = false;
                int newAdminId = clientRoomId - 1;
                if (newAdminId == -1) newAdminId = clientRoomId + 1;
                if (newAdminId == r.clientsInRoom.Count) newAdminId = 0;
                if (r.clientsInRoom.Count > 1)
                {
                    r.clientsInRoom[newAdminId].isAdmin = true;
                    //отправляем уведомление об админстве
                    msgToSend.cmdCommand = Command.setAdmin;
                    byte[] message = msgToSend.ToByte();
                    //while (r.clientsInRoom[newAdminId].isBusy == true) Thread.Sleep(30);
                    if (r.clientsInRoom[newAdminId].socket.Connected)
                    {
                        r.clientsInRoom[newAdminId].acceptEvent.Reset();
                        r.clientsInRoom[newAdminId].socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendOne),
                            new PacketInfo(r.clientsInRoom[newAdminId].socket, Command.setAdmin, -1));
                        r.clientsInRoom[newAdminId].acceptEvent.WaitOne();
                    }
                    else connectionLost("Socket disconnected", r.clientsInRoom[newAdminId].socket);
                    //r.clientsInRoom[newAdminId].isBusy = true;
                    //writeToLog2("Отправлено к " + r.clientsInRoom[newAdminId].login + " " + msgToSend.cmdCommand.ToString());
                }
            }

            if (rooms[clientList[clientId].roomId].status == 1)
            {
                if (r.phase == 5 && r.clientsInRoom.Count > 1) //отключился в этап голосования
                {
                    //делаем дамп его записи
                    ClientInfo clone = new ClientInfo();
                    clone.isLeader = clientList[clientId].isLeader;
                    clone.ip = "LoggedOut";
                    clone.socket = null;
                    clone.voated = clientList[clientId].voated;
                    clone.discarded = clientList[clientId].discarded;
                    clone.login = clientList[clientId].login;
                    clone.roomId = clientList[clientId].roomId;
                    r.clientsInRoom.RemoveAt(clientRoomId);
                    r.clientsInRoom.Insert(clientRoomId, clone);
                    IsNotRemoved = false; //подменили данные чувака, его вынесли из списка клона вставили
                    r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "?"); //типа пометили его как ушедшего но не удалили

                    //проверка готовности, вдруг тот кто ушел - последний
                    bool isReady = true;
                    foreach (ClientInfo cl in r.clientsInRoom)
                    {
                        if (cl.login == clientList[clientId].login) continue;
                        if (cl.login == r.clientsInRoom[findLeaderID(clientId)].login) continue;
                        if (cl.voated <= 0 && cl.ip != "LoggedOut") { isReady = false; break; }
                    }
                    if (isReady)
                    {
                        calcResult(r, clientId, msgToSend);
                    }

                    r.gamersSend = r.gamersSend.Replace("& ", " ");
                }
                if (r.phase == 4 && r.clientsInRoom.Count > 1) //отключился когда сбрасывал карту
                {
                    if (clientList[clientId].isLeader == true)
                    {
                        //делаем дамп его записи
                        ClientInfo clone = new ClientInfo();
                        clone.isLeader = clientList[clientId].isLeader;
                        clone.ip = "LoggedOut";
                        clone.socket = null;
                        clone.voated = clientList[clientId].voated;
                        clone.discarded = clientList[clientId].discarded;
                        clone.login = clientList[clientId].login;
                        clone.roomId = clientList[clientId].roomId;
                        r.clientsInRoom.RemoveAt(clientRoomId);
                        r.clientsInRoom.Insert(clientRoomId, clone);
                        IsNotRemoved = false; //подменили данные чувака, его вынесли из списка клона вставили
                        r.gamersSend = r.gamersSend.Replace(clientList[clientId].login, clientList[clientId].login + "?"); //типа пометили его как ушедшего но не удалили

                    }
                    //проверяем, что был ли он последним не сбросившим
                    bool flag = true;
                    foreach (ClientInfo ci in r.clientsInRoom)
                    {
                        if (ci.login == clientList[clientId].login) continue;
                        if (ci.discarded == 0) { flag = false; break; }//кто-то тоже не скинул
                    }
                    if (flag) //он последний, его и ждали
                    {
                        r.clientsInRoom.RemoveAt(clientRoomId); //удаляем его наконец
                        IsNotRemoved = false;
                        Data msgToSendV = new Data(r.clientsInRoom.Count);
                        msgToSendV.cmdCommand = Command.VoatingTurn;
                        msgToSendV.userCards = ShuffleDiscarded(clientId);
                        Send4All(clientId, Command.VoatingTurn, msgToSendV);
                        r.phase = 5; //все сбросили карты - время для голосования!
                        r.gamersSend = rooms[clientList[clientId].roomId].gamersSend.Replace("# ", " ");
                    }
                }
                //ведущий игрок вышел из комнаты
                if (clientRoomId == findLeaderID(clientId))
                {
                    int curLeader = clientRoomId;
                    if (r.clientsInRoom[clientRoomId].ip != "LoggedOut")
                    {
                        if (r.phase == 2) curLeader++;
                        else curLeader--;
                        if (curLeader == r.clientsInRoom.Count) curLeader = 0;
                        if (curLeader == -1) curLeader = r.clientsInRoom.Count - 1;
                        if (r.clientsInRoom.Count > 1) r.clientsInRoom[curLeader].isLeader = true;
                        r.clientsInRoom[clientRoomId].isLeader = false;
                    }
                    if (r.phase == 2 && r.clientsInRoom.Count > 2)
                    {
                        msgToSend.cmdCommand = Command.LeaderTurn;
                        byte[] message = msgToSend.ToByte();
                        //while (r.clientsInRoom[curLeader].isBusy == true) Thread.Sleep(30);
                        if (r.clientsInRoom[curLeader].socket.Connected)
                        {
                            r.clientsInRoom[curLeader].acceptEvent.Reset();
                            r.clientsInRoom[curLeader].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSendOne), new PacketInfo(r.clientsInRoom[curLeader].socket, Command.LeaderTurn, -1));
                            r.clientsInRoom[curLeader].acceptEvent.WaitOne();

                            msgToSend.cmdCommand = Command.Waiting;
                            msgToSend.login = r.clientsInRoom[curLeader].login;
                            message = msgToSend.ToByte();
                            r.acceptAllEvent.Reset();
                            r.sendCount = 2;
                            for (int i = 0; i < r.clientsInRoom.Count; i++)
                            {
                                if (i == curLeader || i == clientRoomId) continue;
                                if (r.clientsInRoom[i].socket.Connected)
                                    r.clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                        new AsyncCallback(OnSendRoom), new PacketInfo(r.clientsInRoom[i].socket, Command.Waiting, -2));
                                else
                                {
                                    connectionLost("Socket disconnected", r.clientsInRoom[i].socket);
                                    r.acceptAllEvent.Set();
                                }
                            }
                            r.acceptAllEvent.WaitOne();
                            r.gamersSend = r.gamersSend.Replace("# ", " ");
                        }
                        else connectionLost("Socket disconnected", r.clientsInRoom[curLeader].socket);
                        
                    }
                }

                if (r.phase == 6 && r.clientsInRoom.Count > 1) //отключился при ознакомлении с результатами
                {
                    //тут пофиг кто ушел, проверка последнего
                    bool isReady = true;
                    foreach (ClientInfo cl in r.clientsInRoom)
                        if (cl.discarded > 0 && cl.login != clientList[clientId].login) { isReady = false; break; }
                    if (isReady)
                    {
                        r.clientsInRoom.RemoveAt(clientRoomId); //удаляем его наконец
                        IsNotRemoved = false;
                        roundBegin(r, clientId, new Random(), false);
                    }
                }
            }

            //Thread.Sleep(100);
            string test = r.gamers;
            test = test.Replace(" " + clientList[clientId].login + ";", null);
            test = test.Replace(clientList[clientId].login + "; ", null);
            test = test.Replace("; " + clientList[clientId].login, null);
            r.gamers = test;
            r.gamersSend = r.gamersSend.Replace(clientList[clientId].login + " ", null);
            r.gamersSend = r.gamersSend.Replace(clientList[clientId].login + "* ", null);
            r.gamersSend = r.gamersSend.Replace(clientList[clientId].login + "# ", null);
            if (IsNotRemoved) r.clientsInRoom.RemoveAt(clientRoomId); //удаляем его наконец
            playersChanged(false, null); //обновляем таблицу на серваке
            //проверяем есть ли пустые комнаты
            if (r.clientsInRoom.Count == 0)
            {
                int rid = clientList[clientId].roomId;
                rooms.RemoveAt(rid);
                foreach (ClientInfo cl in clientList)
                    if (cl.roomId > rid) cl.roomId--;
            }
            else Send4All(clientId, Command.ListUsers, msgToSend);

        }

        private void roundBegin(Room r, int clientId, Random rnd, bool IsGameStart)
        {
            //выбираем ведущего
            int leaderID = 0;
            if (IsGameStart)
            {
                for (int i = 0; i < r.clientsInRoom.Count; i++)
                    r.clientsInRoom[i].isLeader = false;
                leaderID = rnd.Next(r.clientsInRoom.Count);
            }
            else
            {
                leaderID = findLeaderID(clientId);
                r.clientsInRoom[leaderID].isLeader = false;
                leaderID++;
                if (leaderID == r.clientsInRoom.Count) leaderID = 0;
                r.clientsInRoom[leaderID].isLeader = true;
            }
            playersChanged(false, null);

            Data msgToSend = new Data();
            r.clientsInRoom[leaderID].isLeader = true;

            msgToSend.cmdCommand = Command.LeaderTurn;
            msgToSend.cardID = NextCard(new Random(), clientId);
            byte[] message = msgToSend.ToByte();
            //while (r.clientsInRoom[leaderID].isBusy == true) Thread.Sleep(30);
            r.clientsInRoom[leaderID].acceptEvent.Reset();
            if (r.clientsInRoom[leaderID].socket.Connected)
                r.clientsInRoom[leaderID].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                    new AsyncCallback(OnSendOne), new PacketInfo(r.clientsInRoom[leaderID].socket, Command.LeaderTurn, -1));
            else
            {
                connectionLost("Socket disconnected", r.clientsInRoom[leaderID].socket);
                return;
            }
            r.clientsInRoom[leaderID].acceptEvent.WaitOne();
            //r.clientsInRoom[leaderID].isBusy = true;
            //writeToLog2("Отправлено к " + r.clientsInRoom[leaderID].login + " " + msgToSend.cmdCommand.ToString());
            //остальные ждут когда он загадает


            r.acceptAllEvent.Reset();
            r.sendCount = 1;
            for (int i = 0; i < r.clientsInRoom.Count; i++)
                if (i != leaderID)
                {
                    msgToSend.cmdCommand = Command.Waiting;
                    msgToSend.login = r.clientsInRoom[leaderID].login;
                    msgToSend.cardID = NextCard(new Random(), clientId);
                    message = msgToSend.ToByte();

                    if (r.clientsInRoom[i].socket.Connected)
                        r.clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSendRoom), new PacketInfo(r.clientsInRoom[i].socket, Command.Waiting, r.clientsInRoom.Count));
                    else
                    {
                        connectionLost("Socket disconnected", r.clientsInRoom[i].socket);
                        if (i == r.clientsInRoom.Count - 1) r.acceptAllEvent.Set();
                    }
                    //r.clientsInRoom[i].isBusy = true;
                    //writeToLog2("Отправлено к " + r.clientsInRoom[i].login + " " + msgToSend.cmdCommand.ToString());
                }
            
            r.acceptAllEvent.WaitOne();
            r.phase = 2;

            r.gamersSend = r.gamersSend.Replace("# ", " ");
            r.gamersSend = r.gamersSend.Replace("? ", " ");
        }

        private bool calcResult(Room r, int clientId, Data msgToSend)
        {
                int[] roundScores = new int[r.clientsInRoom.Count];
                string roundResult = "";
                int LeaderId = findLeaderID(clientId);
                r.phase = 6;
                //финальная фаза - подсчет очков
                int loginLong = 0;
                for (int i = 0; i < r.clientsInRoom.Count; i++)
                    if (r.clientsInRoom[i].login.Length > loginLong) loginLong = r.clientsInRoom[i].login.Length;
            
                for (int i = 0; i < r.clientsInRoom.Count; i++)
                {
                    roundScores[i] = 0;
                    if (LeaderId == i)
                    {
                        int count = 0;
                        for (int j = 0; j < r.clientsInRoom.Count; j++)
                        {
                            if (i == j) continue;
                            if (r.clientsInRoom[j].voated == r.clientsInRoom[i].discarded) count++;
                        }
                        if (count == 0 || count == (r.clientsInRoom.Count - 1))
                            roundScores[i] = -2; //все отгадали или никто -2 очка
                        else roundScores[i] = 3;
                    }
                    else
                    {
                        if (r.clientsInRoom[i].voated == r.clientsInRoom[i].discarded)
                            roundScores[i] += -2; //проголосовал за свою -1 очко // +1 за голос за свою
                        if (r.clientsInRoom[i].voated == r.clientsInRoom[LeaderId].discarded)
                            roundScores[i] += 3; //отгадал +3 очка
                        for (int j = 0; j < r.clientsInRoom.Count; j++)
                            if (r.clientsInRoom[j].voated == r.clientsInRoom[i].discarded) roundScores[i] ++;
                    }
                    r.clientsInRoom[i].scores += roundScores[i];
                    roundResult += "   " + r.clientsInRoom[i].login;
                    for (int k = r.clientsInRoom[i].login.Length; k <= loginLong; k++)
                        roundResult += " ";
                    roundResult += "\t\t" + roundScores[i].ToString() + "\n";
                }

                Data msgToSendV = new Data(rooms[clientList[clientId].roomId].clientsInRoom.Count*3);
                for (int i = 0; i < r.clientsInRoom.Count; i++)
                    msgToSendV.userCards[i] = r.clientsInRoom[i].scores;
                for (int i = r.clientsInRoom.Count; i < 2*r.clientsInRoom.Count; i++)
                    msgToSendV.userCards[i] = r.clientsInRoom[i - r.clientsInRoom.Count].discarded;
                for (int i = 2*r.clientsInRoom.Count; i < 3 * r.clientsInRoom.Count; i++)
                    msgToSendV.userCards[i] = r.clientsInRoom[i - 2*r.clientsInRoom.Count].voated;
                msgToSendV.gameToConnectRoomName = roundResult;
                Send4All(clientId, Command.Result, msgToSendV); //отправка результата
                r.gamersSend = r.gamersSend.Replace("# ", " ");
                r.gamersSend = r.gamersSend.Replace("* ", " ");

                bool winnerHere = defineWinner(r, clientId, msgToSend);

                for (int i = 0; i < r.clientsInRoom.Count; i++) //ищем все дампы
                    if (r.clientsInRoom[i].ip == "LoggedOut")
                    {
                        if (r.clientsInRoom[i].isLeader)
                            if (i == 0) r.clientsInRoom[r.clientsInRoom.Count - 1].isLeader = true;
                            else r.clientsInRoom[i - 1].isLeader = true;
                        r.gamersSend = r.gamersSend.Replace(r.clientsInRoom[i].login + "? ", " ");
                        r.clientsInRoom.RemoveAt(i); //удаляем дамп ушедшего
                        i = 0;
                    }
                return winnerHere;
        }

        private bool defineWinner(Room r, int clientId, Data msgToSend)
        {
            for (int i = 0; i < r.clientsInRoom.Count; i++)
                if (r.clientsInRoom[i].scores >= r.maxScores) //Вот он победитель! почти
                {
                    bool flag = true;
                    for (int j = 0; j < r.clientsInRoom.Count; j++)
                        if (r.clientsInRoom[i].scores <= r.clientsInRoom[j].scores && i != j)
                            flag = false;
                    if (flag)
                    {
                        writeToLog("В комнате " + r.roomName + " победил игрок: " + r.clientsInRoom[i].login);
                        msgToSend.login = r.clientsInRoom[i].login;
                        Send4All(clientId, Command.win, msgToSend);
                        //Thread.Sleep(2000);
                        r.status = 0;
                        r.gamersSend = r.gamersSend.Replace(r.clientsInRoom[i].login, r.clientsInRoom[i].login + "&");
                        r.clientsInRoom[findLeaderID(clientId)].isLeader = false;
                        if (roomsChanged != null)
                            roomsChanged(null, null);
                        return true;
                    }
                }
            return false;
        }

        int NextCard(Random rnd, int cId) //генерируем число и извлекаем запись. содержимое записи - номер карты
        {
            var r = rooms[clientList[cId].roomId];
            if (r.Cards.Count == 0)
            {
                for (int i = 0; i < r.deckSize; i++)
                    r.Cards.Add(i + 1);
            }
            int pos = rnd.Next(0, r.Cards.Count);
            int res = r.Cards[pos];
            r.Cards.RemoveAt(pos);
            return res;
        }

        int[] ShuffleDiscarded(int clientID)
        {
            List<int> temp = new List<int>();
            Random rnd = new Random();
            var r = rooms[clientList[clientID].roomId]; //активная комната
            int[] result = new int[r.clientsInRoom.Count];
            for (int i = 0; i < r.clientsInRoom.Count; i++)
                temp.Add(r.clientsInRoom[i].discarded);
            while (temp.Count != 0)
            {
                int randi = rnd.Next(temp.Count);
                result[temp.Count - 1] = temp[randi];
                temp.RemoveAt(randi);
            }
            return result;
        }

        #region writes && findes
        private void writeToLog(string p)
        {
            if (currentLog1 >= logSize)
            {
                if (saveLog1)
                {
                    string Path = "ServLog1 " + System.DateTime.Now.Date.ToShortDateString() + " " + System.DateTime.Now.TimeOfDay.ToString(@"hh\-mm") + ".txt";
                    StreamWriter objWriter = new StreamWriter(@Path);
                    string[] str = log.Split('\n');
                    foreach (string s in str)
                        objWriter.WriteLine(s);
                    objWriter.Close();
                    objWriter.Dispose();
                }
                log = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + p + "\r\n";
                currentLog1 = 0;
            }
            else
                log = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + p + "\r\n" + log;
            currentLog1++;
            if(logChanged!=null)
                logChanged(log, null);
        }

        private void writeToLog2(string text, bool publish)
        {
            if (currentLog2 >= logSize)
            {
                if (saveLog2)
                {
                    string Path = "ServLog2 " + System.DateTime.Now.Date.ToShortDateString() + " " + System.DateTime.Now.TimeOfDay.ToString(@"hh\-mm") + ".txt";
                    StreamWriter objWriter = new StreamWriter(@Path);
                    string[] str = log2.Split('\n');
                    foreach (string s in str)
                        objWriter.WriteLine(s);
                    objWriter.Close();
                    objWriter.Dispose();
                }
                log2 = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + text + "\r\n";
                currentLog2 = 0;
            }
            else log2 = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] " + text + "\r\n" + log2;
            currentLog2++;
            if (log2Changed != null && publish == true)
                log2Changed(log, null);
        }

        private int findClient(List<ClientInfo> clientList, Socket clientSocket)
        {
            int nIndex = 0;
            foreach (ClientInfo client in clientList)
            {
                if (client.socket == clientSocket)
                    return nIndex;
                nIndex++;
            }
            return -1;
        }

        private int findClient(List<ClientInfo> clientList, string login)
        {
            int nIndex = 0;
            foreach (ClientInfo client in clientList)
            {
                if (client.login == login)
                    return nIndex;
                nIndex++;
            }
            return -1;
        }

        private bool isLoginOK(List<ClientInfo> clientList, string login)
        {
            for (int i = 0; i < clientList.Count; i++)
                if (clientList[i].login.StartsWith(login) || login.StartsWith(clientList[i].login)) return false;
            return true;
        }

        private int findRoom(string roomName)
        {
            int nIndex = 0;
            foreach (Room r in rooms)
            {
                if (roomName == r.roomName)
                    return nIndex;
                nIndex++;
            }
            return -1;
        }

        private int findLeaderID(int ClientId)
        {
            int nIndex = 0;
            foreach (ClientInfo ci in rooms[clientList[ClientId].roomId].clientsInRoom)
            {
                if (ci.isLeader) return nIndex;
                nIndex++;
            }
            return -1;
        }
        #endregion

        #region OnSends
        private void OnSendOne(IAsyncResult ar)
        {
            try
            {
                PacketInfo packet = (PacketInfo)ar.AsyncState;
                packet.socket.EndSend(ar);
                int clientId = findClient(clientList, packet.socket);
                clientList[clientId].acceptEvent.Set();
                writeToLog2("Отправлено " + packet.cmd.ToString() + " к " + clientList[clientId].login, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server exeption OnSendOne");
            }
        }

        private void OnSendRoom(IAsyncResult ar)
        {
            try
            {
                PacketInfo packet = (PacketInfo)ar.AsyncState;
                packet.socket.EndSend(ar);
                int clientId = findClient(clientList, packet.socket);
                lock (rooms[clientList[clientId].roomId])
                {
                    rooms[clientList[clientId].roomId].sendCount++;
                    if (rooms[clientList[clientId].roomId].sendCount == rooms[clientList[clientId].roomId].clientsInRoom.Count)
                        rooms[clientList[clientId].roomId].acceptAllEvent.Set();
                    writeToLog2("Отправлено " + packet.cmd.ToString() + " к " + clientList[clientId].login + " " +
                        rooms[clientList[clientId].roomId].sendCount.ToString() + "/" + rooms[clientList[clientId].roomId].clientsInRoom.Count.ToString()
                        + " в \"" + rooms[clientList[clientId].roomId].roomName + "\"" , false);
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server exeption OnSendRoom");
            }
        }

        private void OnSendLobby(IAsyncResult ar)
        {
            try
            {
                PacketInfo packet = (PacketInfo)ar.AsyncState;
                packet.socket.EndSend(ar);
                int clientId = findClient(clientList, packet.socket);
                lock (lobbyLocker)
                {
                    sendLobby++;
                    if (sendLobby == packet.counter) acceptLobbyEvent.Set();
                    writeToLog2("Отправлено " + packet.cmd.ToString() + " к " + clientList[clientId].login + " " +
                        sendLobby.ToString() + "/" + packet.counter.ToString(), false);
                }
                
            } 
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server exeption OnSendLobby");
            }
        }
        #endregion

        internal void sendNewListsOfRooms()
        {
            Data msgToSend = new Data();
            byte[] message;

            msgToSend.cmdCommand = Command.List;
            msgToSend.list = rooms;
            message = msgToSend.ToByte();
            lock (removeLobbyLocker)
            {
                int waitersNum = 0;
                sendLobby = 0;
                
                acceptLobbyEvent.Reset();
                for (int i = 0; i < clientList.Count; i++)
                    if (clientList[i].roomId == -1) waitersNum++;
                if (waitersNum == 0) return;
                for (int i = 0; i < clientList.Count; i++)
                    if (clientList[i].roomId == -1)
                        if (clientList[i].socket.Connected)
                        clientList[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendLobby), 
                            new PacketInfo(clientList[i].socket, Command.List, waitersNum));
                        else connectionLost("Socket disconnected", clientList[i].socket);
                    acceptLobbyEvent.WaitOne();
            }
        }

        internal void sendNewListOfPlayers()
        {
            Data msgToSend = new Data();
            byte[] message;

            msgToSend.cmdCommand = Command.ListWaitingUsers;
            string usrs = "";
            for (int i = 0; i < clientList.Count; i++)
                if (clientList[i].roomId == -1)
                    usrs += clientList[i].login + " ";
            msgToSend.UsersInRoom = usrs;
            message = msgToSend.ToByte();
            lock (removeLobbyLocker)
            {
                int waitersNum = 0;
                sendLobby = 0;
                acceptLobbyEvent.Reset();
                for (int i = 0; i < clientList.Count; i++)
                    if (clientList[i].roomId == -1) waitersNum++;
                if (waitersNum == 0) return;
                for (int i = 0; i < clientList.Count; i++)
                    if (clientList[i].roomId == -1)
                        if (clientList[i].socket.Connected)
                        clientList[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(OnSendLobby), 
                            new PacketInfo(clientList[i].socket, Command.ListWaitingUsers, waitersNum));
                        else connectionLost("Socket disconnected", clientList[i].socket);
                acceptLobbyEvent.WaitOne();
            }
        }

        void Send4All(int clientID, Command cmd, Data msgToSend)
        {
            msgToSend.cmdCommand = cmd;
            msgToSend.UsersInRoom = rooms[clientList[clientID].roomId].gamersSend;
            byte[] message = msgToSend.ToByte();

            rooms[clientList[clientID].roomId].sendCount = 0;
            rooms[clientList[clientID].roomId].acceptAllEvent.Reset();
            //отправляем всем кто в комнате

            for (int i = 0; i < rooms[clientList[clientID].roomId].clientsInRoom.Count; i++)
            {
                if (rooms[clientList[clientID].roomId].clientsInRoom[i].socket != null)
                {
                    if (rooms[clientList[clientID].roomId].clientsInRoom[i].socket.Connected)
                        rooms[clientList[clientID].roomId].clientsInRoom[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                        new AsyncCallback(OnSendRoom), new PacketInfo(rooms[clientList[clientID].roomId].clientsInRoom[i].socket, cmd, -2));
                    else
                    {
                        connectionLost("Socket disconnected", rooms[clientList[clientID].roomId].clientsInRoom[i].socket);
                        rooms[clientList[clientID].roomId].acceptAllEvent.Set();
                    }
                }
                else
                    lock (rooms[clientList[clientID].roomId])
                    {
                        rooms[clientList[clientID].roomId].sendCount++;
                        writeToLog2("Отправлено " + cmd + " к " + clientList[i].login + " " +
                            rooms[clientList[clientID].roomId].sendCount + "/" + rooms[clientList[clientID].roomId].clientsInRoom.Count.ToString() + " socket = null", false);
                    }
            }
            rooms[clientList[clientID].roomId].acceptAllEvent.WaitOne();
            
        }

        public void SendServerShutdown()
        {
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.Null;
            byte[] message = msgToSend.ToByte();

            for (int i = 0; i < clientList.Count; i++)
            {
                //while (clientList[i].isBusy == true) Thread.Sleep(30);
                clientList[i].acceptEvent.Reset();
                clientList[i].socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                    new AsyncCallback(OnSendOne), new PacketInfo(clientList[i].socket, Command.Null, -3));
                clientList[i].acceptEvent.WaitOne();
                //writeToLog2("Отправлено к " + clientList[i].login + " " + msgToSend.cmdCommand.ToString());
            }

            string Path = "ServLog1Closed " + System.DateTime.Now.Date.ToShortDateString() + " " + System.DateTime.Now.TimeOfDay.ToString(@"hh\-mm") + ".txt";
            StreamWriter objWriter = new StreamWriter(@Path);
            string[] str = log.Split('\n');
            foreach (string s in str)
                objWriter.WriteLine(s);
            objWriter.Close();
            objWriter.Dispose();
            Path = "ServLog2Closed " + System.DateTime.Now.Date.ToShortDateString() + " " + System.DateTime.Now.TimeOfDay.ToString(@"hh\-mm") + ".txt";
            StreamWriter objWriter2 = new StreamWriter(@Path);
            string[] str2 = log2.Split('\n');
            foreach (string s in str2)
                objWriter2.WriteLine(s);
            objWriter2.Close();
            objWriter2.Dispose();

            Thread.Sleep(2000);
        }

        void timer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < clientList.Count; i++)
                if (clientList[i].socket.Connected == false)
                    lock (globalLocker)
                        connectionLost("Socket disconnected", clientList[i].socket);
        }

        #endregion
    }
}
