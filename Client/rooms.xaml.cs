using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Serg.WPF;

namespace DixitClient
{
    /// <summary>
    /// Логика взаимодействия для rooms.xaml
    /// </summary>
    public partial class rooms : SergWindow
    {
        MainWindow w;
        TextBlock txt;
        //public bool IsSoundEnabled;
        public bool passed;
        public event EventHandler soundPlay;
        int deckSize = 252;
        int chatSize = 100;
        int currentSize = 0;
        private class User
        {
            public string login { get; set; }

            public User(string _login)
            {
                login = _login;
            }
        }
        List<User> UsersWaiting = new List<User>();

        public rooms(MainWindow wnd, double opacity)
        {
            InitializeComponent();
            w = wnd;
            w.client.refreshRooms += client_refreshRooms;
            w.client.refreshWaitingUsers += new EventHandler(client_refreshWaitingUsers);
            w.client.chatMsgWaiting += new EventHandler(client_chatMsgWaiting);
            txt = new TextBlock();
            txt.FontWeight = FontWeights.Bold;
            txt.Text = "Выберите комнату!";
            this.Tag = txt;
            //IsSoundEnabled = wnd.IsSoundEnabled;
            UsersGridBackgroundBrush.Opacity = opacity;
            ChatBackgroundBrush.Opacity = opacity;
            RoomsGridBackgroundBrush.Opacity = opacity;
        }

        void client_chatMsgWaiting(object sender, EventArgs e)
        {
            string input = (string)sender;
            string[] splitter = { "!splitter!" };
            string[] msg = input.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
            ChatBox.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (w.IsSoundEnabled) w.playSound(new Uri(@"sounds\\message.mp3", UriKind.Relative));

                    if (currentSize == chatSize) //чат переполнен
                    {
                        ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили время
                        ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили имя
                        ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили сообщение
                        currentSize--;
                    }
                    string time = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm") + "] ";
                    Span DateSpan = new Span();
                    DateSpan.Inlines.Add(time);
                    DateSpan.FontWeight = FontWeights.Bold;
                    DateSpan.Foreground = Brushes.DarkGreen;
                    ChatBox.Inlines.Add(DateSpan);

                    
                    Span UserNameSpan = new Span();
                    UserNameSpan.Inlines.Add(msg[0] + ": ");
                    UserNameSpan.Foreground = Brushes.DarkBlue;
                    DateSpan.FontWeight = FontWeights.Bold;
                    ChatBox.Inlines.Add(UserNameSpan);
                    ChatBox.Inlines.Add(msg[1] + "\n");

                    scrollBox.UpdateLayout();
                    scrollBox.ScrollToBottom();
                    currentSize++;
                }));
        }

        void client_refreshWaitingUsers(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)( () =>
            {
                string[] usrs = (sender as string).Split(' ');
                this.GamersDataGrid.Items.Clear();
                UsersWaiting.Clear();
                for (int i = 0; i < usrs.Length; i++)
                    if (usrs[i] != "") UsersWaiting.Add(new User(usrs[i]));
                for (int i = 0; i < UsersWaiting.Count; i++)
                    GamersDataGrid.Items.Add(UsersWaiting[i]);
            }));
        }

        void client_refreshRooms(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                roomsDataGrid.Items.Clear();
                for (int i = 0; i < w.client.list.Count; i++)
                    roomsDataGrid.Items.Add(w.client.list[i]);
            }));
        }

        private void onClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Disconnect;
                w.client.Send(msgToSend);
                Application app = Application.Current;
                app.Shutdown();
            }));
        }

        private void createGame_Click(object sender, RoutedEventArgs e)
        {
            if (roomNameBox.Text == "") 
            {
                txt.Text = "Невозможно создать комнату с таким именем!";
                if (w.IsSoundEnabled)
                    soundPlay(new Uri(@"sounds\\error.mp3", UriKind.Relative), null);
                    return;
            }
            if (roomNameBox.Text.Length > 15)
            {
                txt.Text = "Максимальная длина названия комнаты - 15 символов!";
                if (w.IsSoundEnabled)
                    soundPlay(new Uri(@"sounds\\error.mp3", UriKind.Relative), null);
                return;
            }
            if (pwdBox.Text.Length > 20)
            {
                txt.Text = "Максимальная длина пароль 20 символов, вот так.";
                if (w.IsSoundEnabled)
                    soundPlay(new Uri(@"sounds\\error.mp3", UriKind.Relative), null);
                return;
            }
            //проверка уникальности имени комнаты
            foreach (Room r in w.client.list)
            {
                if (r.roomName == roomNameBox.Text )
                {
                    expander.IsExpanded = true;
                    if (w.IsSoundEnabled)
                        soundPlay(new Uri(@"sounds\\error.mp3", UriKind.Relative), null);
                    txt.Text = "Невозможно создать комнату с таким именем!";
                    return;
                }
            }
            //отправка создания комнаты
            w.Title += " [" + roomNameBox.Text + " | " + (int)Math.Round(sliderScores.Value) + " ]";
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.newGame;
            msgToSend.gameToConnectRoomName = roomNameBox.Text;
            msgToSend.login = pwdBox.Text;
            msgToSend.cardID = (int)Math.Round(sliderScores.Value);
            msgToSend.usersInRoom = deckSize.ToString();
            if (!w.client.Send(msgToSend))
            {
                this.Close();
                return;
            }
            //w.client.createGame(, , ,);
            w.isAdmin = true;
            this.Hide();
            w.Show();
            w.buttonTaskOK.Content = "Начать игру";
            w.buttonTaskOK.IsEnabled = true;
            if (w.IsSoundEnabled)
                soundPlay(new Uri(@"sounds\\connect.mp3", UriKind.Relative), null);
        }

        private void connectToRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Room selecterdRoom = (Room)roomsDataGrid.SelectedItem;
                if (selecterdRoom.status > 0)
                {
                    txt.Text = "Игра уже началась! Вы не можете подключиться";
                    if (w.IsSoundEnabled) soundPlay(new Uri(@"sounds\\warning.mp3", UriKind.Relative), null);
                    return;
                }
                if (selecterdRoom.status == 0)
                {
                    passed = false;
                    if (selecterdRoom.password == "") passed = true;
                    else
                    {
                        pass p = new pass(this, selecterdRoom.password);
                        p.ShowDialog();
                    }


                    if (passed)
                    {
                        w.isAdmin = false;
                        Data msgToSend = new Data();
                        msgToSend.cmdCommand = Command.connectToGame;
                        msgToSend.gameToConnectRoomName = selecterdRoom.roomName;

                        if (!w.client.Send(msgToSend))
                        {
                            this.Close();
                            return;
                        }

                        this.Hide();
                        w.Show();
                        w.buttonTaskOK.IsEnabled = false;
                        w.buttonTaskOK.Content = "Ожидание. . .";
                        w.Title += " [" + selecterdRoom.roomName + " | " + selecterdRoom.maxScores.ToString() + " ]";
                        if (w.IsSoundEnabled)
                            soundPlay(new Uri(@"sounds\\connect.mp3", UriKind.Relative), null);
                    }
                    else
                    {
                        txt.Text = "Неверный пароль!";
                        if (w.IsSoundEnabled) soundPlay(new Uri(@"sounds\\warning.mp3", UriKind.Relative), null);
                    }
                }
                
                
            }
            catch { }
        }

        private void sliderScores_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BoxScores.Text = Math.Round(sliderScores.Value).ToString();
        }

        private void roomNameBox_MouseEnter(object sender, MouseEventArgs e)
        {
            if (roomNameBox.Text == "Новая комната")
            {
                roomNameBox.Text = "";
                roomNameBox.FontStyle = FontStyles.Normal;
            }
        }

        private void SergWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                roomsDataGrid.Items.Clear();
                for (int i = 0; i < w.client.list.Count; i++)
                    roomsDataGrid.Items.Add(w.client.list[i]);
            }));
        }

        private void buttonComplect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (txtComplect.Text == "Оффициальные карты Dixit 1, 2, Odyssey")
            {
                txtComplect.Text = "Оффициальные карты Dixit 1, 2, Odyssey + карты игроков";
                deckSize = 999;
            }
            else
            {
                txtComplect.Text = "Оффициальные карты Dixit 1, 2, Odyssey";
                deckSize = 252;
            }
        }

        private void buttonChat_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxChat.Text != "")
            {
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.chat;
                msgToSend.cardID = 0;
                msgToSend.usersInRoom = textBoxChat.Text;
                if (!w.client.Send(msgToSend))
                {
                    Connection c = new Connection();
                    c.Show();
                    this.Close();
                    return;
                }
                textBoxChat.Text = "";
            }
        }

        private void buttonSound_Click(object sender, RoutedEventArgs e)
        {
            if (w.IsSoundEnabled)
                buttonSoundText.Text = "V";
            else
            {
                buttonSoundText.Text = "U";
                soundPlay(new Uri(@"sounds\\beep.mp3", UriKind.Relative), null);
            }
            w.IsSoundEnabled = !w.IsSoundEnabled;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            helper h = new helper(this, w);
            h.ShowDialog();
        }
    }
}
