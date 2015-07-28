using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DixitServer
{

    class Data
    {
        public int cardID;
        public int[] userCards;
        public string login;
        public List<Room> list;
        public string gameToConnectRoomName;
        public string UsersInRoom;
        public Command cmdCommand;

        //Default constructor
        public Data()
        {
            this.cmdCommand = Command.Null;
            login="";
            gameToConnectRoomName = "";
            list = new List<Room>();
            userCards = new int[6];
        }
        public Data(int n)
        {
            this.cmdCommand = Command.Null;
            login = "";
            gameToConnectRoomName = "";
            list = new List<Room>();
            userCards = new int[n];
        }

        //Converts the bytes into an object of type Data
        public Data(byte[] data)
        {
            //The first four bytes are for the Command
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);

            switch (this.cmdCommand)
            {
                case Command.newGame:
                    cardID = BitConverter.ToInt32(data, 4);
                    int roomLen = BitConverter.ToInt32(data, 8);
                        gameToConnectRoomName = Encoding.Unicode.GetString(data, 12, roomLen);
                    int pswLen = BitConverter.ToInt32(data, 12 + roomLen);
                        login = Encoding.Unicode.GetString(data, 16 + roomLen, pswLen);
                    UsersInRoom = BitConverter.ToInt32(data, 16+roomLen+pswLen).ToString();
                    break;
                case Command.Connect:
                    int loginLen = BitConverter.ToInt32(data, 4);
                    if (loginLen > 0)
                    {
                        login = Encoding.Unicode.GetString(data, 8, loginLen);
                        cardID = BitConverter.ToInt32(data, 8+loginLen);
                    }
                    break;

                case Command.connectToGame:
                    int gameRoomNameLen = BitConverter.ToInt32(data, 4);
                    if(gameRoomNameLen>0)
                        gameToConnectRoomName = Encoding.Unicode.GetString(data, 8, gameRoomNameLen);
                    break;

                case Command.LeaderTurn:
                    cardID = BitConverter.ToInt32(data, 4);
                    int taskLen = BitConverter.ToInt32(data, 8);
                    gameToConnectRoomName =  Encoding.Unicode.GetString(data, 12, taskLen);
                    break;
                case Command.GamersTurn:
                    cardID = BitConverter.ToInt32(data, 4);
                    break;
                case Command.VoatingTurn:
                    cardID = BitConverter.ToInt32(data, 4);
                    break;
                case Command.chat:
                    cardID = BitConverter.ToInt32(data, 4);
                    taskLen = BitConverter.ToInt32(data, 8);
                    UsersInRoom = Encoding.Unicode.GetString(data, 12, taskLen);
                    break;

            }

        }

        //Converts the Data structure into an array of bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>();

            //First four are for the Command
            result.AddRange(BitConverter.GetBytes((int)cmdCommand));

            switch (cmdCommand)
            {
                case Command.Connect:
                    if (login != null)
                    {
                        result.AddRange(BitConverter.GetBytes(login.Length*2));
                        result.AddRange(Encoding.Unicode.GetBytes(login));
                    }
                    else
                        result.AddRange(BitConverter.GetBytes(0));

                    break;
                case Command.startGame:
                    for (int i = 0; i < 5; i++)
                        result.AddRange(BitConverter.GetBytes(userCards[i]));
                    result.AddRange(BitConverter.GetBytes(cardID));
                    break;

                case Command.List:
                    result.AddRange(BitConverter.GetBytes(list.Count));
                    if (list.Count > 0)
                    {
                        foreach (Room l in list)
                        {
                            result.AddRange(BitConverter.GetBytes(l.roomName.Length*2));
                            result.AddRange(Encoding.Unicode.GetBytes(l.roomName));
                            result.AddRange(BitConverter.GetBytes(l.maxScores));
                            result.AddRange(BitConverter.GetBytes(l.status));
                            result.AddRange(BitConverter.GetBytes(l.gamers.Length*2));
                            result.AddRange(Encoding.Unicode.GetBytes(l.gamers));
                            result.AddRange(BitConverter.GetBytes(l.deckSize));
                            result.AddRange(BitConverter.GetBytes(l.password.Length * 2));
                            result.AddRange(Encoding.Unicode.GetBytes(l.password));
                        }
                    }
                    break;

                case Command.ListUsers:
                    result.AddRange(BitConverter.GetBytes(UsersInRoom.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(UsersInRoom));
                    break;

                case Command.ListWaitingUsers:
                    result.AddRange(BitConverter.GetBytes(UsersInRoom.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(UsersInRoom));
                    break;

                case Command.connectToGame:
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;
                case Command.LeaderTurn:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    break;
                case Command.Waiting:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    result.AddRange(BitConverter.GetBytes(login.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(login));
                    break;
                case Command.GamersTurn:
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;
                case Command.VoatingTurn:
                    result.AddRange(BitConverter.GetBytes(userCards.Length));
                    for (int i = 0; i < userCards.Length; i++)
                        result.AddRange(BitConverter.GetBytes(userCards[i]));
                    break;
                case Command.Result:
                    result.AddRange(BitConverter.GetBytes(userCards.Length));
                    for (int i=0; i<userCards.Length; i++)
                        result.AddRange(BitConverter.GetBytes(userCards[i]));
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length * 2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;

                case Command.win:
                    result.AddRange(BitConverter.GetBytes(login.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(login));
                    break;
                case Command.chat:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    result.AddRange(BitConverter.GetBytes(login.Length * 2));
                    result.AddRange(Encoding.Unicode.GetBytes(login));
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length * 2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;
                
            }

            return result.ToArray();
        } 
    }

}
