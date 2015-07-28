using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Globalization;

namespace DixitClient
{

    class Data
    {
        public int cardID;
        public int[] userCards;
        public string login;
        public List<Room> list;
        public string gameToConnectRoomName;
        public string usersInRoom;
        public Command cmdCommand;

        //Default constructor
        public Data()
        {
            this.cmdCommand = Command.Null;
            login="";
            gameToConnectRoomName = "";
            userCards = new int[6];
        }

        //Converts the bytes into an object of type Data
        public Data(byte[] data)
        {
            //The first four bytes are for the Command
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);

            switch (this.cmdCommand)
            {
                case Command.Connect:
                    int loginLen = BitConverter.ToInt32(data, 4);
                    login = Encoding.Unicode.GetString(data, 8, loginLen);

                    break;
                case Command.startGame:
                    userCards = new int[5];
                    for (int i = 0; i < 5; i++)
                        userCards[i] = BitConverter.ToInt32(data, 4 + 4*i);
                    cardID = BitConverter.ToInt32(data, 4 + 4 * 5);
                    break;
                
                case Command.connectToGame:
                    int gameRoomNameLen = BitConverter.ToInt32(data, 4);
                    if(gameRoomNameLen>0)
                        gameToConnectRoomName = Encoding.Unicode.GetString(data, 8, gameRoomNameLen);
                    break;
                case Command.List:
                    int listCount = BitConverter.ToInt32(data, 4);
                    if (listCount > 0)
                    {
                        int pos = 8;
                        list = new List<Room>();
                        for (int i = 0; i < listCount; i++)
                        {
                            Room l = new Room();
                            int roomNameLen = BitConverter.ToInt32(data, pos);
                            l.roomName = Encoding.Unicode.GetString(data, pos + 4, roomNameLen);
                            l.maxScores = BitConverter.ToInt32(data, pos + 4 + roomNameLen);
                            l.status = BitConverter.ToInt32(data, pos + 4 + roomNameLen + 4);
                            int gamersLen = BitConverter.ToInt32(data, pos + 4 + roomNameLen + 4 + 4);
                            l.gamers = Encoding.Unicode.GetString(data, pos + 4 + roomNameLen + 12, gamersLen);
                            l.numGamers = 1;
                            foreach (char c in l.gamers) if (c == ';') l.numGamers++;
                            list.Add(l);
                            l.deckSize = BitConverter.ToInt32(data, pos + 4 + roomNameLen + 12 + gamersLen);
                            int pwdLen = BitConverter.ToInt32(data, pos + 4 + roomNameLen + 16 + gamersLen);
                            l.password = Encoding.Unicode.GetString(data, pos + 4 + roomNameLen + 20 + gamersLen, pwdLen);
                            pos = pos + 4 + roomNameLen + 20 + gamersLen + pwdLen;
                        }
                    }
                    break;
                case Command.ListUsers:
                    int strLen = BitConverter.ToInt32(data, 4);
                    usersInRoom = Encoding.Unicode.GetString(data, 8, strLen);
                    break;
                case Command.ListWaitingUsers:
                    strLen = BitConverter.ToInt32(data, 4);
                    usersInRoom = Encoding.Unicode.GetString(data, 8, strLen);
                    break;
                case Command.LeaderTurn:
                    cardID = BitConverter.ToInt32(data, 4);
                    break;
                case Command.Waiting:
                    cardID = BitConverter.ToInt32(data, 4);
                    int strLog = BitConverter.ToInt32(data, 8);
                    login = Encoding.Unicode.GetString(data, 12, strLog);
                    break;
                case Command.GamerTurn:
                    strLen = BitConverter.ToInt32(data, 4);
                    gameToConnectRoomName = Encoding.Unicode.GetString(data, 8, strLen);
                    break;
                case Command.VoatingTurn:
                    int size = BitConverter.ToInt32(data, 4);
                    userCards = new int[size];
                    for (int i = 0; i < size; i++)
                        userCards[i] = BitConverter.ToInt32(data, 8 + 4 * i);
                    break;
                case Command.Result:
                    int sz = BitConverter.ToInt32(data, 4);
                    userCards = new int[sz];
                    for (int i = 0; i < sz; i++)
                        userCards[i] = BitConverter.ToInt32(data, 8 + 4 * i);
                    strLen = BitConverter.ToInt32(data, 8+4*sz);
                    gameToConnectRoomName = Encoding.Unicode.GetString(data, 12+4*sz, strLen);
                    break;

                case Command.win:
                    strLog = BitConverter.ToInt32(data, 4);
                    login = Encoding.Unicode.GetString(data, 8, strLog);
                    break;
                case Command.chat:
                    cardID = BitConverter.ToInt32(data, 4);
                    strLen = BitConverter.ToInt32(data, 8);
                    login = Encoding.Unicode.GetString(data, 12, strLen);
                    int strLen2 = BitConverter.ToInt32(data, 12 + strLen);
                    gameToConnectRoomName = Encoding.Unicode.GetString(data, 16 + strLen, strLen2);
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
                case Command.newGame:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    result.AddRange(BitConverter.GetBytes(login.Length * 2));
                    result.AddRange(Encoding.Unicode.GetBytes(login));
                    result.AddRange(BitConverter.GetBytes(Convert.ToInt32(usersInRoom)));
                    break;
                case Command.Connect:
                    if (login != null)
                    {
                        result.AddRange(BitConverter.GetBytes(login.Length*2));
                        result.AddRange(Encoding.Unicode.GetBytes(login));
                        result.AddRange(BitConverter.GetBytes(cardID));
                    }
                    else
                        result.AddRange(BitConverter.GetBytes(0));

                    break;
                case Command.connectToGame:
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;  
                case Command.List:
                    result.AddRange(BitConverter.GetBytes(0));
                    break;
                case Command.LeaderTurn:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    result.AddRange(BitConverter.GetBytes(gameToConnectRoomName.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(gameToConnectRoomName));
                    break;
                case Command.GamerTurn:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    break;
                case Command.VoatingTurn:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    break;
                case Command.chat:
                    result.AddRange(BitConverter.GetBytes(cardID));
                    result.AddRange(BitConverter.GetBytes(usersInRoom.Length*2));
                    result.AddRange(Encoding.Unicode.GetBytes(usersInRoom));
                    break;
            }

            return result.ToArray();
        } 
    }
}
