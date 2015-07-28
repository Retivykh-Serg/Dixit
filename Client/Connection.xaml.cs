using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using Serg.WPF;

namespace DixitClient
{
    /// <summary>
    /// Логика взаимодействия для Connection.xaml
    /// </summary>
    public partial class Connection : SergWindow
    {
        MainWindow w;
        messageViewer m;
        public TextBlock txt;
        int port;
        public bool IsOk = true;
        byte[] ip = new byte[4];
        public Client client;
        public double opacity = 0.95;
        MediaPlayer player;
        public bool IsSoundEnabled = true;

        public Connection()
        {
            InitializeComponent();
            txt = new TextBlock();
            txt.FontWeight = FontWeights.Bold;
            txt.Text = "Здарова!";
            txt.Margin = new Thickness(-2, 0, 0, 0);
            this.Tag = txt;
            m = new messageViewer();
            player = new MediaPlayer();
            //m.Show();
        }

        private void conectButton_Click(object sender, RoutedEventArgs e)
        {
            if (loginTextBox.Text == "")
            {
                txt.Text = "Введите свой логин!";
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative));
                return;
            }
            if (loginTextBox.Text.Contains<char>(' ') || loginTextBox.Text.Contains<char>('?') || loginTextBox.Text.Contains<char>('#') ||
                loginTextBox.Text.Contains<char>('*') || loginTextBox.Text.Contains<char>('&') || loginTextBox.Text.Contains<char>(';'))
            {
                txt.Text = "Логин не может содержать пробелы и символы & # * ? ;";
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative));
                return;
            }
            try { port = Convert.ToInt32(portTextBox.Text); }
            catch { 
                txt.Text = "Некорректный порт!"; 
                if (IsSoundEnabled) 
                    playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative)); 
                return; }

            try
            {
                string[] temp = ipTextBox.Text.Split('.');
                for (int i = 0; i < 4; i++)
                    ip[i] = Convert.ToByte(temp[i]);
            }
            catch { 
                txt.Text = "Некорректный адрес!";
                if (IsSoundEnabled)
                    playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative)); 
                return; }

            if (loginTextBox.Text.Length > 15)
            {
                txt.Text = "Максимальная длина логина 15 символов";
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative));
                return;
            }

            if (loginTextBox.Text.Length < 3)
            {
                txt.Text = "Минимальная длина логина 3 символа";
                if (IsSoundEnabled) playSound(new Uri(@"sounds\\error.mp3", UriKind.Relative));
                return;
            }

            client = new Client(loginTextBox.Text, ip, port, this, m);
            client.connectSuccess += new EventHandler(client_connectSuccess);
            client.connect();

        }

        void client_connectSuccess(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                txt.Text = "Успешное подключение";
                if (IsSoundEnabled)
                    playSound(new Uri(@"sounds\\connect.mp3", UriKind.Relative)); 
                w = new MainWindow(loginTextBox.Text, client, opacity, IsSoundEnabled, m);
                m.Dispatcher.Invoke((Action)(() =>
                    {
                        m.SetLogin(loginTextBox.Text);
                    }));
                this.Close();
            }));
        }

        private void SergWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Height = 180;
            this.Width = 370;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            helper h = new helper(this);
            h.ShowDialog();
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
        
    }
}
