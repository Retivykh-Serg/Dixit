using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Documents;

namespace DixitClient
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
        GamerTurn,
        VoatingTurn,
        Result,

        Success,
        Failed,
        logOutRoom,
        win,
        chat,
        setAdmin,
        OldVersion,
        Null
    }

    public class Room
    {
        public int id { get; set; }
        public string roomName { get; set; }
        public int status { get; set; }
        public int maxScores { get; set; }
        public int numGamers { get; set; }
        public string gamers { get; set; }
        public int deckSize { get; set; }
        public string password;

        public string Status
        {
            get
            {
                if (status == 0)
                    return "Ожидание";
                return "Идет игра";
            }
            set
            {
                status = 1;
                if (value == "Ожидание")
                    status = 0;
            }
        }

        public string Pwd
        {
            get
            {
                if (password == "")
                    return "Нет";
                return "Да";
            }
            set
            {
                password = value;
            }
        }
        
    }

    
   
    public class Client
    {
        int VERSION = 1005;//версия клиента!!!
        Connection c;
        messageViewer m;
        private string login;
        private byte[] ip;
        private int port;
        public Socket clientSocket;
        private byte[] byteData = new byte[1024];
        public List<Room> list = new List<Room>(0);
        private bool isAccepted = false;

        public event EventHandler needExit;
        public event EventHandler refreshRooms;
        public event EventHandler refreshWaitingUsers;
        public event EventHandler gameStart;
        public event EventHandler refreshUsersInRooms;
        public event EventHandler leaderTurn;
        public event EventHandler waiting;
        public event EventHandler gamerTurn;
        public event EventHandler voatingTurn;
        public event EventHandler result;
        public event EventHandler resultChat;
        //public event EventHandler newRound;
        public event EventHandler win;
        public event EventHandler serverDisconnect;
        public event EventHandler chatMsg;
        public event EventHandler chatMsgWaiting;
        public event EventHandler setAdmin;
        public event EventHandler connectSuccess;

        public Client(string _login, byte[] _ip, int _port, Connection _C, messageViewer _m)
        {
            login = _login;
            ip = _ip;
            port = _port;
            c = _C;
            m = _m;
        }

        public void connect()
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = new IPAddress(ip);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
            //Connect to the server
            clientSocket.BeginConnect(ipEndPoint, new AsyncCallback(OnConnect), null);
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndConnect(ar);

                //We are connected so we login into the server
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Connect;
                msgToSend.login = login;
                msgToSend.cardID = VERSION;

                byte[] b = msgToSend.ToByte();

                //Send the message to the server
                clientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
                m.Dispatcher.Invoke((Action)(() =>
                {
                    m.AddMsg("Отправлено " + msgToSend.cmdCommand.ToString() + "\n", false);
                }));
                getListRooms();

                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
            }
            catch (Exception)
            {
                //MessageBox.Show(ex.Message, "Exeption connect");
                c.Dispatcher.Invoke((Action)(() =>
               {
                   c.txt.Text = "Cервер недоступен :(";
                   if (c.IsSoundEnabled) c.playSound(new Uri(@"sounds\\beep.mp3", UriKind.Relative)); 
               }));
                
                // if (needExit != null) needExit(null, null);
            }
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndSend(ar);
                m.Dispatcher.Invoke((Action)(() =>
                {
                    m.AddMsg("Отправлено " + (ar.AsyncState as string) + "\n", false);
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Exeption");
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndReceive(ar);
                
                //Transform the array of bytes received from the user into an
                //intelligent form of object Data
                Data msgReceived = new Data(byteData);
                //packets += " " + msgReceived.cmdCommand.ToString();
                //if (login == "Serg") MessageBox.Show(packets);
                //We will send this object in response the users request
                Data msgToSend = new Data();
                m.Dispatcher.Invoke((Action)(() =>
                {
                    m.AddMsg("Получено " + msgReceived.cmdCommand.ToString() + " " + msgReceived.login + "\n", true);
                }));
                
                switch (msgReceived.cmdCommand)
                {
                    case Command.List:
                        if (msgReceived.list != null)
                            list = msgReceived.list;
                        else
                            list = new List<Room>(0);
                            refreshRooms(null, null);
                        break;
                    case Command.ListUsers:
                        if (refreshUsersInRooms != null)
                            refreshUsersInRooms(msgReceived.usersInRoom, null);
                        break;
                    case Command.ListWaitingUsers:
                        refreshWaitingUsers(msgReceived.usersInRoom, null);
                        break;

                    case Command.chat:
                        if (chatMsg != null)
                        {
                            string msg = msgReceived.login;
                            msg += "!splitter!";
                            msg += msgReceived.gameToConnectRoomName;
                            if (msgReceived.cardID == 1) chatMsg(msg, null);
                            else chatMsgWaiting(msg, null);
                        }
                        break;
                    case Command.startGame:
                        if (gameStart != null)
                        {
                            int[] temp = new int[6];
                            for (int i = 0; i < 5; i++) temp[i] = msgReceived.userCards[i];
                            temp[5] = msgReceived.cardID;
                            gameStart(temp, null);
                        }
                        break;
                    case Command.LeaderTurn:
                        if (leaderTurn != null) leaderTurn(msgReceived.cardID, null);
                        break;
                    case Command.Waiting:
                        if (waiting != null) waiting(msgReceived, null);
                        break;
                    case Command.GamerTurn:
                        if (gamerTurn != null) gamerTurn(msgReceived.gameToConnectRoomName, null);
                        break;
                    case Command.VoatingTurn:
                    if (voatingTurn!= null) voatingTurn(msgReceived.userCards, null);
                        break;
                    case Command.Result:
                        if (result != null) result(msgReceived.userCards, null);
                        if (resultChat != null) resultChat(msgReceived.gameToConnectRoomName, null);
                        break;

                    case Command.setAdmin:
                        if (setAdmin != null) setAdmin(null, null);
                        break;
                    case Command.win:
                        if (win != null) win(msgReceived.login, null);
                        break;
                    case Command.Success:
                        //Первый раз: при подтверждении логина - запрос списка комнат
                        //MessageBox.Show("Сервер подтвердил подключение");
                        connectSuccess(null, null);
                        msgToSend.cmdCommand = Command.List;
                        byte[] b = msgToSend.ToByte();
                        clientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), msgToSend.cmdCommand.ToString());
                        isAccepted = true;
                        
                        break;

                    case Command.Failed:
                        //в зависимости от предыдущей комманды
                        //MessageBox.Show("Сервер запретил подключение", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                        c.Dispatcher.Invoke((Action)(() =>
                        {
                            c.txt.Text = "Игрок с похожим логином уже есть на сервере! :(";
                            if (c.IsSoundEnabled) c.playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative)); 
                        }));
                        // if (needExit != null) needExit(null, null);
                        break;
                    case Command.OldVersion:
                        c.Dispatcher.Invoke((Action)(() =>
                        {
                            c.txt.Text = "Вы используете старую версию клиента!";
                            if (c.IsSoundEnabled) c.playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative));
                        }));
                        break;

                    case Command.Null: //сервак при закрытии отправил всем
                        //MessageBox.Show("Сервер сообщил о критической ошибке. Соединение разорвано", "Ошибка");
                        needExit(null, null);
                        break;

                    case Command.Disconnect: //кикнули из комнаты
                        //MessageBox.Show("Отключение", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                        if (serverDisconnect != null) serverDisconnect(null, null);
                        break;
                }

                if (msgReceived.cmdCommand != Command.Failed && msgReceived.cmdCommand != Command.Null)
                    clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка приёма. Исключение");
                needExit(null, null);
            }
        }

        public List<Room> getListRooms()
        {
            if (list != null)
                return list;
            else 
                return null;
        }

        internal bool Send(Data msgToSend)
        {
            if (isAccepted && clientSocket.Connected)
            {
                byte[] b = msgToSend.ToByte();
                //Send the message to the server
                clientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), msgToSend.cmdCommand.ToString());
                return true;
            }
            else
            {
                MessageBox.Show("Соединение с сервером потеряно!", "Ошибка передачи данных");
                clientSocket.Close();
                clientSocket.Dispose();
                return false;
            }
        }
    }
}
