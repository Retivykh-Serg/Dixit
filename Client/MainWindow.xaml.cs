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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Globalization;
using Serg.WPF;



namespace DixitClient
{
    public class Gamer
    {

        public string login { get; set; }
        public int status { get; set; }
        public int scores { get; set; }
        public SolidColorBrush color { get; set; }
        private Color[] colorVariants = {Colors.DodgerBlue, Colors.Red, Colors.LimeGreen, Colors.Yellow, Colors.MediumVioletRed,
                                            Colors.Orange, Colors.CadetBlue, Colors.SpringGreen, Colors.IndianRed, Colors.Chartreuse, 
                                            Colors.DarkKhaki, Colors.SaddleBrown, Colors.White, Colors.Navy, Colors.Black};

        public Gamer(string _login, int i)
        {
            login = _login;
            status = 0;
            scores = 0;
            if (i < 15)
                color = new SolidColorBrush(colorVariants[i]);
            else
            {
                Random rnd = new Random();
                color = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255)));
            }
        }
    }
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : SergWindow
    {
        messageViewer m;
        public rooms r;
        public Client client;
        int[] cards;
        int[] voating;
        List<Gamer> gamers;
        bool isLeader, gameStarted = false;
        string LeaderName;
        public string myLogin;
        public bool isAdmin = false;
        TextBlock tag;
        int round = 1;
        int status = 0; //0 ждем начало - 1 игра началась - 2 LeaderTurn - 3 clientWaiting 
        // 4 GamerTurn - 5 VoatingTurn 6 - result
        MediaPlayer player;
        public bool IsSoundEnabled;
        object locker = new object();

        int chatSize = 100;
        int currentSize = 0;

        public MainWindow(string login, Client cl, double opacity, bool snd, messageViewer _m)
        {
            InitializeComponent();
            
            myLogin = login;
            this.Title = "Dixit - " + myLogin;
            client = cl;
            client.needExit += client_needExit;
            client.gameStart += client_gameStart;
            client.refreshUsersInRooms += client_refreshUsersInRooms;
            client.leaderTurn += client_leaderTurn;
            client.waiting += client_waiting;
            client.gamerTurn += client_gamerTurn;
            client.voatingTurn += client_voatingTurn;
            client.result += client_result;
            client.resultChat += client_resultChat;
            //client.newRound +=client_newRound;
            client.win+=client_win;
            client.serverDisconnect += client_serverDisconnect;
            client.chatMsg += client_chatMsg;
            client.setAdmin += client_setAdmin;

            player = new MediaPlayer();
            IsSoundEnabled = snd;
            if (!IsSoundEnabled) buttonSoundText.Text = "V";
            m = _m;
            r = new rooms(this, opacity);
            r.soundPlay += new EventHandler(r_soundPlay);
            r.Show();
            this.Hide();
            textBoxTask.Visibility = System.Windows.Visibility.Hidden;
            gamers = new List<Gamer>();
            Gamer alone = new Gamer(myLogin, 0);
            GamersdataGrid.Items.Add(alone);
            tag = new TextBlock();
            tag.Text = "Ожидание начала игры";
            tag.FontWeight = FontWeights.Bold;
            tag.FontStyle = FontStyles.Italic;
            tag.TextAlignment = TextAlignment.Center;
            tag.FontSize = 14;
            Tag = tag;
            userCardsPanel.Visibility = System.Windows.Visibility.Hidden;
            VoatingPanel.Visibility = System.Windows.Visibility.Hidden;
            userCardsPanelBorder.Visibility = System.Windows.Visibility.Hidden;
            VoatingPanelBorder.Visibility = System.Windows.Visibility.Hidden;
            SetCardSample();
            LinearGradientBrush myBrush = new LinearGradientBrush(Colors.PowderBlue, Colors.MediumSlateBlue, 30);
            ImgBrdr.Background = myBrush;
            VoatingBackgroundBrush.Opacity = opacity;
            ChatBackgroundBrush.Opacity = opacity;
            UsersGridBackgroundBrush.Opacity = opacity;
            
        }

        void r_soundPlay(object sender, EventArgs e)
        {
            playSound(sender as Uri);
        }

        //начало партии
        void client_gameStart(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                SetCardSample();
                userCardsPanel.Visibility = System.Windows.Visibility.Visible;
                VoatingPanel.Visibility = System.Windows.Visibility.Visible;
                userCardsPanelBorder.Visibility = System.Windows.Visibility.Visible;
                VoatingPanelBorder.Visibility = System.Windows.Visibility.Visible;
                TaskBlock.Visibility = System.Windows.Visibility.Hidden;
                VoatingPanel.Children.Clear();
                userCardsPanel.Children.Clear();

                cards = (int[])sender;
                cards[5] = 0;
                for (int i = 0; i < 6; i++)
                {
                    RadioButton tmp = new RadioButton();
                    tmp.Checked += radioButton_Checked;
                    tmp.Focusable = false;
                    tmp.Tag = cards[i].ToString();
                    Uri uri = new Uri(Environment.CurrentDirectory+"\\img\\"+cards[i].ToString()+".jpg");
                    BitmapImage imgSource = new BitmapImage(uri);
                    Image img = new Image();
                    img.Source = imgSource;
                    tmp.Content = img;
                    img.Stretch = Stretch.Uniform;
                    userCardsPanel.Children.Add(tmp);
                    Grid.SetColumn(tmp, i);
                }
                cards[5] = -1;
                gameStarted = true;
                round = 1;
                AddChatMsg(0, null, "Игра началась!\nКоличество очков для победы " + cards[5] + "\n", Brushes.DarkSlateBlue);
                AddChatMsg(0, null, "Раунд " + round.ToString() + "\n", Brushes.DarkGreen);

                GamersdataGrid.Items.Clear();
                for (int i = 0; i < gamers.Count; i++)
                {
                    gamers[i].scores = 0;
                    GamersdataGrid.Items.Add(gamers[i]);
                }

                status = 1;
            }));
        }

        void client_leaderTurn(object sender, EventArgs e)
        {
            while (status == 0)  Thread.Sleep(50);
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\leader.mp3", UriKind.Relative));

                prepareNewRound((int)sender);
                status = 2;
                isLeader = true;
                LeaderName = myLogin;
                textBoxTask.Visibility = System.Windows.Visibility.Visible;
                textBoxTask.IsEnabled = true;
                textBoxTask.Text = "Напишите здесь свою ассоциацию";
                textBoxTask.FontStyle = FontStyles.Italic;
                textBoxTask.FontWeight = FontWeights.Normal;
                buttonTaskOK.IsEnabled = true;
                (Tag as TextBlock).Text = "Загадайте ассоциацию!";
                buttonTaskOK.Content = "Загадать";

            }));
        }

        void client_waiting(object sender, EventArgs e)
        {
            while (status == 0) Thread.Sleep(50);
            lock (locker)
                Dispatcher.Invoke((Action)(() =>
                {
                    if (IsSoundEnabled) playSound(new Uri(@"sounds\\waiting.mp3", UriKind.Relative));
                    
                    Data input = (Data)sender;
                    LeaderName = input.login;
                    int cardID = input.cardID;
                    prepareNewRound(cardID);
                    status = 3;
                    isLeader = false;
                    buttonTaskOK.IsEnabled = false;
                    (Tag as TextBlock).Text = "Игрок " + LeaderName + " загадывает ассоциацию...";
                    AddChatMsg(0, null, "Игрок " + LeaderName + " загадывает ассоциацию...\n", Brushes.DarkGreen);
                    textBoxTask.Visibility = System.Windows.Visibility.Hidden;
                    buttonTaskOK.Content = "Ожидание. . .";
                }));
        }

        void client_gamerTurn(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\beep.mp3", UriKind.Relative));
                TaskBlock.Text = LeaderName + ": " + (string)sender;
                TaskBlock.Focus();
                TaskBlock.Visibility = Visibility.Visible;
                buttonTaskOK.IsEnabled = true;
                (Tag as TextBlock).Text = "Ассоциация загадана!";
                status = 4;
                buttonTaskOK.Content = "Отправить карту";
                SetCardVoatingPanel(0, LeaderName, 1);
            }));
        }

        void client_voatingTurn(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\voating.mp3", UriKind.Relative));
                VoatingPanel.Children.Clear();
                voating = (int[])sender;
                for (int i = 0; i < voating.Length; i++)
                    SetCardVoatingPanel(voating[i], (i+1).ToString(), 0);
                if (isLeader) buttonTaskOK.IsEnabled = false;
                else buttonTaskOK.IsEnabled = true;
                (Tag as TextBlock).Text = "Голосование!";
                status = 5;
                buttonTaskOK.Content = "Голосовать";
            }));
        }

        void client_result(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                int[] result = (int[])sender;
                for (int i = 0; i < gamers.Count; i++)
                {
                    gamers[i].scores = result[i];
                    int numOfGamer = 0;
                    for (int j = gamers.Count; j < 2 * gamers.Count; j++)
                        if (voating[i] == result[j]) numOfGamer = j - gamers.Count;
                        
                        ((VoatingPanel.Children[i] as DockPanel).Children[0] as TextBlock).Text = gamers[numOfGamer].login;
                        if (gamers[numOfGamer].login == LeaderName)
                            ((VoatingPanel.Children[i] as DockPanel).Children[0] as TextBlock).Foreground = gamers[numOfGamer].color;
                }
                for (int i = 2 * gamers.Count; i < 3 * gamers.Count; i++)
                {
                    for (int j = 0; j < gamers.Count; j++)
                    {
                        if (((VoatingPanel.Children[j] as DockPanel).Children[1] as RadioButton).Tag.ToString() == result[i].ToString())
                        {
                            Border sample = new Border();
                            sample.BorderThickness = new Thickness(1);
                            sample.BorderBrush = Brushes.Black;
                            sample.CornerRadius = new CornerRadius(1);
                            sample.Margin = new Thickness(2);
                            sample.Background = gamers[i - 2*gamers.Count].color;
                            sample.MaxWidth = 20;
                            sample.MaxHeight = 20;
                            sample.MinWidth = 10;
                            sample.MinHeight = 10;
                            sample.ToolTip = gamers[i - 2 * gamers.Count].login;
      
                            sample.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                            sample.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                            
                            ((VoatingPanel.Children[j] as DockPanel).Children[2] as WrapPanel).Children.Add(sample);
                        }
                    }
                }
                if (status != 0)
                {
                    buttonTaskOK.IsEnabled = true;
                    (Tag as TextBlock).Text = "Результаты!";
                    status = 6;
                    buttonTaskOK.Content = "Следующий раунд";
                }
                lock(locker)
                foreach (Gamer g in gamers) if (g.status == -1) gamers.Remove(g); //удаляем мертвые души
            }));
        }

        void client_resultChat(object sender, EventArgs e)
        {
            lock (locker)
            ChatBox.Dispatcher.Invoke((Action)(() =>
            {
                AddChatMsg(2, null, (string)sender, null);
            }));
        }

        void prepareNewRound(int cardID)
        {
                foreach (Gamer g in gamers)
                    if (g.status == 5) gamers.Remove(g);
                VoatingPanel.Children.Clear();
                SetCardSample();
                TaskBlock.Visibility = Visibility.Hidden;
                TaskBlock.Text = "";
                textBoxTask.Text = "";
                for (int i = 0; i < 6; i++)
                {
                    if ((userCardsPanel.Children[i] as RadioButton).IsChecked == true)
                        (userCardsPanel.Children[i] as RadioButton).IsChecked = false;
                    if (cards[i] == -1)
                    {
                        cards[i] = cardID;
                        RadioButton tmp = new RadioButton();
                        tmp.Checked += radioButton_Checked;
                        tmp.Focusable = false;
                        tmp.Tag = cards[i].ToString();
                        Uri uri = new Uri(Environment.CurrentDirectory + "\\img\\" + (string)tmp.Tag + ".jpg");
                        BitmapImage imgSource = new BitmapImage(uri);
                        Image img = new Image();
                        img.Source = imgSource;
                        img.Stretch = Stretch.Uniform;
                        tmp.Content = img;
                        userCardsPanel.Children.RemoveAt(i);
                        userCardsPanel.Children.Insert(i, tmp);
                        Grid.SetColumn(tmp, i);
                    }
                }
                
                foreach (Gamer g in gamers)
                    g.status = 0;
                GamersdataGrid.Items.Clear();
                for (int i = 0; i < gamers.Count; i++)
                    GamersdataGrid.Items.Add(gamers[i]);
                VoatingPanel.Children.Clear();
                if (cardID != 0)
                {
                    round++;
                    AddChatMsg(0, null, "Раунд " + round.ToString() + "\n", Brushes.DarkGreen);
                }
                status = 1;
        }

        void client_win(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\win.mp3", UriKind.Relative));
                string winner = (string)sender;
                if (winner == myLogin) (Tag as TextBlock).Text = "Вы победили! Ууууууууууууууууууууууурррррррррррррааааааааааааааааааааа";
                else (Tag as TextBlock).Text = "Победил " + winner;
                status = 0;
                if (isAdmin)
                {
                    buttonTaskOK.IsEnabled = true;
                    buttonTaskOK.Content = "Начать новую игру";
                }
                else
                {
                    buttonTaskOK.IsEnabled = false;
                    buttonTaskOK.Content = "Ожидание . . .";
                }
                AddChatMsg(0, null, "Игра окончена. Победил " + winner + "\n", Brushes.MediumVioletRed);
                SetCardSample(9998, winner);
            }));
        }

        void client_chatMsg(object sender, EventArgs e)
        {
            lock (locker)
            ChatBox.Dispatcher.Invoke((Action)(() =>
            {
                string input = (string)sender;
                string[] splitter = {"!splitter!"};
                string[] msg = input.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                AddChatMsg(1, msg[0], msg[1], null);              
            }));
        }

        void client_setAdmin(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                isAdmin = true;
                playSound(new Uri(@"sounds\\beep.mp3", UriKind.Relative));
                if (status == 0)
                {
                    buttonTaskOK.IsEnabled = true;
                    buttonTaskOK.Content = "Начать игру!";
                    AddChatMsg(0, null, "Вы стали администратором комнаты, можете начать игру!", Brushes.Firebrick);
                }
                else AddChatMsg(0, null, "Вы стали администратором комнаты!", Brushes.Firebrick);
            }));
        }
        //список игроков для комнаты
        void client_refreshUsersInRooms(object sender, EventArgs e)
        {
            
            Dispatcher.Invoke((Action)(() =>
            {
                lock (locker)
                {
                    if (!gameStarted) gamers.Clear();

                    string temp = (string)sender;
                    string[] usrs = temp.Split(' ');
                    if (gameStarted)
                        foreach (string s in usrs)
                        {
                            //проверка, никого не удалили ли
                            for (int i = 0; i < gamers.Count; i++)
                                if (temp.IndexOf(gamers[i].login) == -1)
                                {
                                    AddChatMsg(0, null, "Игрок " + gamers[i].login + " покинул комнату\n", Brushes.Firebrick);
                                    gamers.Remove(gamers[i]);
                                }
                            for (int j = 0; j < gamers.Count; j++)
                                if (s.StartsWith(gamers[j].login))
                                {
                                    if (s.Length <= 1) continue;
                                    if (s.Contains<char>('*')) gamers[j].status = 2; //загадывал
                                    else if (s.Contains<char>('#'))
                                    {
                                        if ((status == 4 || status == 2) && gamers[j].status == 0) SetCardVoatingPanel(0, gamers[j].login, 1);
                                        gamers[j].status = 1; //прислал карту 
                                    }
                                    else if (s.Contains<char>('&'))
                                    {
                                        gamers[j].status = 3; //выиграл! 
                                        gameStarted = false;
                                    }
                                    else if (s.Contains<char>('?'))
                                    {
                                        gamers[j].status = -1; //отключился в важную фазу, засранец!
                                        //AddChatMsg(0, null, "Игрок " + gamers[j].login + " покинул комнату\n", Brushes.Firebrick);
                                    }
                                    else gamers[j].status = 0;
                                }
                        }
                    else for (int i = 0; i < usrs.Length; i++)
                        {
                            if (usrs[i].Length < 1) continue;
                            Gamer g;
                            if (usrs[i].Contains<char>('*'))
                            {
                                g = new Gamer(usrs[i].Replace('*', ' '), i);
                                g.status = 2; //загадывал
                            }
                            else if (usrs[i].Contains<char>('#'))
                            {
                                g = new Gamer(usrs[i].Replace('#', ' '), i);
                                g.status = 1; //прислал карту
                            }
                            else g = new Gamer(usrs[i], i);
                            gamers.Add(g);
                        }
                    GamersdataGrid.Items.Clear();
                    for (int i = 0; i < gamers.Count; i++)
                        if (gamers[i].status != -1) GamersdataGrid.Items.Add(gamers[i]);
                }
            }));
        }

        void client_serverDisconnect(object sender, EventArgs e)
        {
            lock (locker)
            Dispatcher.Invoke((Action)(() =>
            {
                Button_Click_1(null, null);
            }));
        }

        void client_needExit(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                Application app = Application.Current;
                app.Shutdown();
            }));
        }

        private void Window_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            lock (locker)
                Dispatcher.Invoke((Action)(() =>
                {
                    player.Close();
                    m.Close();
                    Data msgToSend = new Data();
                    msgToSend.cmdCommand = Command.Disconnect;
                    client.Send(msgToSend);
                    Application app = Application.Current;
                    app.Shutdown();
                }));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Hide();
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.logOutRoom;
            if (!client.Send(msgToSend))
            {
                this.Close();
                return;
            }
            if (IsSoundEnabled) playSound(new Uri(@"sounds\\beep.mp3", UriKind.Relative));
            (Tag as TextBlock).Text = "Ожидание игроков";
            userCardsPanel.Children.Clear();
            VoatingPanel.Children.Clear();
            textBoxTask.Text = "";
            gamers.Clear();
            GamersdataGrid.Items.Clear();
            Gamer alone = new Gamer(myLogin, 0);
            GamersdataGrid.Items.Add(alone);
            textBoxTask.Visibility = System.Windows.Visibility.Hidden;
            userCardsPanel.Visibility = System.Windows.Visibility.Hidden;
            VoatingPanel.Visibility = System.Windows.Visibility.Hidden;
            userCardsPanelBorder.Visibility = System.Windows.Visibility.Hidden;
            VoatingPanelBorder.Visibility = System.Windows.Visibility.Hidden;
            SetCardSample();
            ChatBox.Text = "";
            TaskBlock.Text = "";
            gameStarted = false;
            status = 0;
            round = 1;
            this.Title = "Dixit - " + myLogin;
            r.Show();
        }

        private void buttonTaskOK_Click(object sender, RoutedEventArgs e)
        {
            if (status == 0)
            {
                if (GamersdataGrid.Items.Count == 1)
                {
                    SetCardSample(9999, "");
                    if (IsSoundEnabled) playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative));
                    return;
                }
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.startGame;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                }
                return;
            }
            if (status == 2)
            {
                if (textBoxTask.Text.Length < 3 || textBoxTask.Text == "Плохая ассоциация! :(" || textBoxTask.Text == "Напишите здесь свою ассоциацию")
                {
                    (Tag as TextBlock).Text = "Загадайте нормальную ассоциацию!";
                    textBoxTask.Text = "Плохая ассоциация! :(";
                    textBoxTask.FontStyle = FontStyles.Italic;
                    textBoxTask.FontWeight = FontWeights.Normal;
                    if (IsSoundEnabled) playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative));
                    return;
                }
                int flag = -1;
                for (int i = 0; i < 6; i++)
                    if ((userCardsPanel.Children[i] as RadioButton).IsChecked == true)
                        flag = i;
                if (flag == -1)
                {
                    (Tag as TextBlock).Text = "Выберите карту!";
                    if (IsSoundEnabled) playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative));
                    foreach (RadioButton r in userCardsPanel.Children)
                    {
                        r.Focusable = true;
                        r.Focus();
                        r.Focusable = false;
                    }
                    return;
                }
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.LeaderTurn;
                msgToSend.cardID = cards[flag];
                msgToSend.gameToConnectRoomName = textBoxTask.Text;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                    return;
                }

                buttonTaskOK.IsEnabled = false;
                textBoxTask.IsEnabled = false;
                textBoxTask.Visibility = System.Windows.Visibility.Hidden;
                TaskBlock.Text = textBoxTask.Text;
                TaskBlock.Visibility = System.Windows.Visibility.Visible;
                TaskBlock.Focus();
                (Tag as TextBlock).Text = "Ожидание ответа игроков";
                SetCardVoatingPanel(0, LeaderName, 1);
                cards[flag] = -1; //типа сбросили карту там пусто
                
            }
            if (status == 4)
            {
                int flag = -1;
                for (int i = 0; i < 6; i++)
                    if ((userCardsPanel.Children[i] as RadioButton).IsChecked == true)
                        flag = i;
                if (flag == -1)
                {
                   (Tag as TextBlock).Text = "Выберите карту!";
                   if (IsSoundEnabled) playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative));
                   foreach (RadioButton r in userCardsPanel.Children)
                   {
                       r.Focusable = true;
                       r.Focus();
                       r.Focusable = false;
                   }
                   return;
                }
                buttonTaskOK.IsEnabled = false;

                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.GamerTurn;
                msgToSend.cardID = cards[flag];
                if (!client.Send(msgToSend))
                {
                    this.Close();
                    return;
                }

                cards[flag] = -1; //типа сбросили карту там пусто
                (Tag as TextBlock).Text = "Карта отправлена! Ожидание игроков";
                return;
            }
            if (status == 5)
            {
                int flag = -1;
                for (int i = 0; i < VoatingPanel.Children.Count; i++)
                    if (((VoatingPanel.Children[i] as DockPanel).Children[1] as RadioButton).IsChecked == true)
                        flag = Convert.ToInt32(((VoatingPanel.Children[i] as DockPanel).Children[1] as RadioButton).Tag); 
                if (flag == -1) 
                {
                    for (int i = 0; i < VoatingPanel.Children.Count; i++)
                    {
                        ((VoatingPanel.Children[i] as DockPanel).Children[1] as RadioButton).Focusable = true;
                        ((VoatingPanel.Children[i] as DockPanel).Children[1] as RadioButton).Focus();
                        ((VoatingPanel.Children[i] as DockPanel).Children[1] as RadioButton).Focusable = false;
                    }
                    if (IsSoundEnabled) playSound(new Uri(@"sounds\\warning.mp3", UriKind.Relative));
                    (Tag as TextBlock).Text = "Выберите карту!";
                    return;
                }
                buttonTaskOK.IsEnabled = false;

                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.VoatingTurn;
                msgToSend.cardID = flag;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                    return;
                }

                (Tag as TextBlock).Text = "Ожидание голосования игроков";
                return;
            }
            if (status == 6)
            {
                buttonTaskOK.IsEnabled = false;
                (Tag as TextBlock).Text = "Ожидание готовности игроков";

                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Success;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                    return;
                }
                return;
            }
            if (status == 8)
            {
                buttonTaskOK.IsEnabled = false;
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.startGame;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                }
                return;
            }

        }

        private void SetCardVoatingPanel(int card, string title, int type)
        {
            RadioButton tmp = new RadioButton();
            tmp.Checked += radioButton_Checked;
            tmp.Tag = card.ToString();
            tmp.Focusable = false;
            tmp.GroupName = "voating";
            Uri uri = new Uri(Environment.CurrentDirectory + "\\img\\" + card.ToString() + ".jpg");
            BitmapImage imgSource = new BitmapImage(uri);
            Image img = new Image();
            img.Source = imgSource;
            tmp.Content = img;
            tmp.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            img.Stretch = Stretch.Uniform;

            TextBlock header = new TextBlock();
            if (type == 0) header.Text = "Карта " + title;
            else header.Text = title;
            header.FontWeight = FontWeights.Bold;
            header.FontSize = 14;
            header.TextTrimming = TextTrimming.CharacterEllipsis;
            header.UseLayoutRounding = false;
            header.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            header.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            header.LayoutTransform = new RotateTransform(-90);

            TextOptions.SetTextFormattingMode(header, TextFormattingMode.Display);
            DockPanel VoatingItem = new DockPanel();
            VoatingItem.LastChildFill = true;
            VoatingItem.Children.Add(header);
            DockPanel.SetDock(header, Dock.Left);

            VoatingItem.Children.Add(tmp);
            DockPanel.SetDock(tmp, Dock.Left);

            WrapPanel colorPanel = new WrapPanel();
            colorPanel.Orientation = Orientation.Vertical;
            colorPanel.Margin = new Thickness(-23, 10, 0, 5);

            DockPanel.SetDock(colorPanel, Dock.Left);
            VoatingItem.Children.Add(colorPanel);

            VoatingPanel.Children.Add(VoatingItem);
            if (VoatingPanel.Children.Count > 12) VoatingPanel.Rows = 4;
            else if (VoatingPanel.Children.Count > 8) VoatingPanel.Rows = 3;
            else if (VoatingPanel.Children.Count > 2) VoatingPanel.Rows = 2;
            else VoatingPanel.Rows = 1;
        }

        private void SetCardSample()
        {
                Uri uri = new Uri(Environment.CurrentDirectory + "\\img\\0.jpg");
                BitmapImage imgSource = new BitmapImage(uri);
                imageLarge.Source = imgSource;
                imageLarge.Stretch = Stretch.Uniform;
        }

        private void SetCardSample(int card, string login)
        {
                Uri uri = new Uri(Environment.CurrentDirectory + "\\img\\" + card.ToString() + ".jpg");
                BitmapImage bitmap = new BitmapImage(uri);
                DrawingVisual visual = new DrawingVisual();

                int size = 84;
                int offsetX = 130;
                int offsetY = 360;

                if (login.Length == 4) { size = 78;  offsetX = 115; }
                if (login.Length == 5) { size = 78; offsetX = 85; offsetY = 365; }
                if (login.Length == 6) { size = 72; offsetX = 65; offsetY = 370; }
                if (login.Length == 7) { size = 66; offsetX = 60; offsetY = 375; }
                if (login.Length == 8) { size = 60; offsetX = 35; offsetY = 380; }
                if (login.Length == 9) { size = 54; offsetX = 40; offsetY = 385; }
                if (login.Length == 10) { size = 48; offsetX = 40; offsetY = 390; }
                if (login.Length == 11) { size = 42; offsetX = 40; offsetY = 395; }
                if (login.Length == 12) { size = 42; offsetX = 35; offsetY = 395; }
                if (login.Length == 13) { size = 36; offsetX = 30; offsetY = 395; }
                if (login.Length == 14) { size = 36; offsetX = 25; offsetY = 400; }
                if (login.Length == 15) { size = 36; offsetX = 20; offsetY = 400; }


                FormattedText text = new FormattedText(
                    login,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe Script"),
                    size,
                    Brushes.DarkBlue);
                
                
                FormattedText header = new FormattedText(
                    "Победитель",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe Script"),
                    48,
                    Brushes.DarkBlue);

                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                    dc.DrawText(text, new Point(offsetX, offsetY));
                    
                    dc.PushTransform(new RotateTransform(-10, 60, 280));
                    if (card != 9999) dc.DrawText(header, new Point(60, 280));
                }

                RenderTargetBitmap target = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight,
                                                                   bitmap.DpiX, bitmap.DpiY, PixelFormats.Default);
                target.Render(visual);
                
                imageLarge.Source = BitmapFrame.Create(target);
                imageLarge.Stretch = Stretch.Uniform;
        }

        private void AddChatMsg(int type, string login, string msg, Brush clr)
        {
            string time = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm") + "] ";
            
            if (currentSize == chatSize) //чат переполнен
            {
                ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили время
                ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили имя
                ChatBox.Inlines.Remove(ChatBox.Inlines.FirstInline); //удалили сообщение
                currentSize--;
            }

            Span DateSpan = new Span();
            DateSpan.Inlines.Add(time);
            DateSpan.FontWeight = FontWeights.Bold;
            DateSpan.Foreground = Brushes.DarkGreen;
            ChatBox.Inlines.Add(DateSpan);
            if (type == 1)
            {
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\message.mp3", UriKind.Relative));
                Span UserNameSpan = new Span();
                UserNameSpan.Inlines.Add(login + ": ");
                UserNameSpan.Foreground = Brushes.DarkBlue;
                DateSpan.FontWeight = FontWeights.Bold;
                ChatBox.Inlines.Add(UserNameSpan);
                ChatBox.Inlines.Add(msg + "\n");
            }
            if (type == 0) //сообщение сервера
            {
                Span ServerMsg = new Span();
                ServerMsg.Inlines.Add(msg);
                ServerMsg.FontWeight = FontWeights.Bold;
                ServerMsg.Foreground = clr;
                ChatBox.Inlines.Add(ServerMsg);
                ChatBox.Inlines.Add("");
            }
            if (type == 2) // итоги раунда
            {
                Span ResultText = new Span();
                ResultText.Inlines.Add("Результаты раунда: ");
                ResultText.FontStyle = FontStyles.Italic;
                ResultText.Foreground = Brushes.ForestGreen;
                ResultText.FontWeight = FontWeights.Bold;
                ChatBox.Inlines.Add(ResultText);
                Span ResultSpan = new Span();
                ResultSpan.Inlines.Add("\n" + msg);
                ResultSpan.Foreground = Brushes.ForestGreen;
                ResultSpan.FontWeight = FontWeights.SemiBold;
                ChatBox.Inlines.Add(ResultSpan);
                if (IsSoundEnabled)
                {
                    string[] splitter = { "\t\t", "\n" };
                    string[] words = msg.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                    for (int i=0; i<words.Length; i++)
                        if (words[i].Contains(myLogin))
                        {
                            if (Convert.ToInt32(words[i+1]) < 1)
                                playSound(new Uri(@"sounds\\round_1.mp3", UriKind.Relative));
                            else if (Convert.ToInt32(words[i+1]) < 4)
                                playSound(new Uri(@"sounds\\round_2.mp3", UriKind.Relative));
                            else playSound(new Uri(@"sounds\\round_3.mp3", UriKind.Relative));
                            break;
                        }
                }
                
            }
            scrollBox.UpdateLayout();
            scrollBox.ScrollToBottom();
        }

        private void GamersdataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Gamer gamer = (Gamer)e.Row.DataContext;
            if (gamer.status == 2) e.Row.Background = new SolidColorBrush(Color.FromRgb((byte)255, (byte)193, (byte)79));
            if (gamer.status == 1) e.Row.Background = Brushes.LightGreen;
            if (gamer.status == 3) e.Row.Background = Brushes.Gold;
        }

        private void radioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton tmp = (RadioButton)sender;

            Uri uri = new Uri(Environment.CurrentDirectory + "\\img\\" + (string)tmp.Tag + ".jpg");
            BitmapImage imgSource = new BitmapImage(uri);
            imageLarge.Source = imgSource;
            imageLarge.Stretch = Stretch.Uniform;

        }

        private void buttonChat_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxChat.Text != "")
            {
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.chat;
                msgToSend.usersInRoom = textBoxChat.Text;
                msgToSend.cardID = 1;
                if (!client.Send(msgToSend))
                {
                    this.Close();
                    return;
                }
                textBoxChat.Text = "";
            }
        }

        private void textBoxTask_MouseEnter(object sender, MouseEventArgs e)
        {
            if (textBoxTask.Text == "Плохая ассоциация! :(" || textBoxTask.Text == "Напишите здесь свою ассоциацию")
            {
                textBoxTask.Text = "";
                textBoxTask.FontStyle = FontStyles.Normal;
                textBoxTask.FontWeight = FontWeights.Bold;
            }
        }

        private void buttonHelper_Click(object sender, RoutedEventArgs e)
        {
            helper h = new helper(this);
            h.Show();
        }

        public void playSound(Uri file)
        {
            player.Open(file);
            player.Play();
        }

        private void buttonSound_Click(object sender, RoutedEventArgs e)
        {
            if (IsSoundEnabled)
                buttonSoundText.Text = "V";
            else
            {
                buttonSoundText.Text = "U";
                playSound(new Uri(@"sounds\\beep.mp3", UriKind.Relative));
            }
            IsSoundEnabled = !IsSoundEnabled;
        }

        private void borderHelper_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            m.Show();
        }

    }

}
