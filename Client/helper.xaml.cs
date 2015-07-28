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
using System.IO;
using Serg.WPF;

namespace DixitClient
{
    /// <summary>
    /// Логика взаимодействия для helper.xaml
    /// </summary>
    public partial class helper : SergWindow
    {
        rooms r;
        MainWindow w;
        Connection c;
        bool IsMainWindow;
        TextBlock txt = new TextBlock();
        int[] property = new int[7];

        public helper(MainWindow _w)
        {
            IsMainWindow = true;
            w = _w;
            InitializeComponent();
            Initial(w.VoatingBackgroundBrush.Opacity);
        }

        public helper(Connection _c)
        {
            IsMainWindow = false;
            c = _c;
            InitializeComponent();
            Initial(0.95);
        }

        public helper(rooms _r, MainWindow _w)
        {
            IsMainWindow = true;
            w = _w;
            r = _r;
            InitializeComponent();
            Initial(_w.VoatingBackgroundBrush.Opacity);
        }

        private void Initial(double opacity)
        {
            Tag = txt;
            try
            {
                StreamReader objReader = new StreamReader(@"FormFigures.txt");
                string str = objReader.ReadLine();
                objReader.Close();
                string[] settings = str.Split('_');
                for (int i = 0; i < 7; i++)
                    property[i] = Convert.ToInt32(settings[i]);
                slider1.Value = property[0];
                slider2.Value = property[1];
                slider3.Value = property[2];
                slider4.Value = property[3];
                slider5.Value = property[4];
                textBlock4.Text = property[3].ToString();
                textBlock5.Text = property[4].ToString();
                slider4.Maximum = property[2];
                slider5.Maximum = property[2];
                slider0.Value = opacity;
                if (property[5] == 0) border1text.Text = "Масштабировать";
                if (property[5] == 1) border1text.Text = "Заполнить";
                if (property[5] == 2) border1text.Text = "Масштабировать до заполнения";
                if (property[6] == 0) border2text.Text = "Заливка выключена";
                if (property[6] == 1) border2text.Text = "Заливка включена";
                slider4.TickFrequency = (int)Math.Round((double)(property[2] / 10));
                slider5.TickFrequency = (int)Math.Round((double)(property[2] / 10));
                txt.Text = "Читайте внимательно";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка файла конфигурации окна FormFigures.txt\n" + ex.Message);
                this.Close();
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            StreamWriter sw = new StreamWriter(@"FormFigures.txt", false);
            string s = property[0] + "_" + property[1] + "_" + property[2] + "_" + property[3] + 
                "_" + property[4] + "_" + property[5] + "_" + property[6];
            sw.WriteLine(s);
            sw.Close();
            txt.Text = "Настройки окна сохранены!";
            double m = Math.Round(slider0.Value, 2);
            if (!IsMainWindow) c.opacity = m;
            if (IsMainWindow)
            {
                if (r == null)
                {
                    w.r.UsersGridBackgroundBrush.Opacity = m;
                    w.r.ChatBackgroundBrush.Opacity = m;
                    w.r.RoomsGridBackgroundBrush.Opacity = m;
                }
                else
                {
                    r.UsersGridBackgroundBrush.Opacity = m;
                    r.ChatBackgroundBrush.Opacity = m;
                    r.RoomsGridBackgroundBrush.Opacity = m;
                }
                
                w.ChatBackgroundBrush.Opacity = m;
                w.UsersGridBackgroundBrush.Opacity = m;
                w.VoatingBackgroundBrush.Opacity = m;
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int m = (int)Math.Round(slider1.Value);
            textBlock1.Text = m.ToString();
            property[0] = m;
            txt.Text = "Сохранено!";
            txt.Text = "Выполните необходимые настройки";
        }

        private void slider2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int m = (int)Math.Round(slider2.Value);
            textBlock2.Text = Math.Round(slider2.Value).ToString();
            property[1] = m;
            txt.Text = "Выполните необходимые настройки";
        }

        private void slider3_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int max = (int)Math.Round(slider3.Value);
            textBlock3.Text = max.ToString();
            slider4.Maximum = max;
            slider4.TickFrequency = (int)Math.Round((double)(max / 10));
            slider5.TickFrequency = (int)Math.Round((double)(max / 10));
            slider5.Maximum = max;
            property[2] = max;
            txt.Text = "Выполните необходимые настройки";
        }

        private void slider4_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int m = (int)Math.Round(slider4.Value);
            textBlock4.Text = m.ToString();
            property[3] = m;
            txt.Text = "Выполните необходимые настройки";
        }

        private void slider5_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int m = (int)Math.Round(slider5.Value);
            textBlock5.Text = m.ToString();
            property[4] = m;
            txt.Text = "Выполните необходимые настройки";
        }

        private void border1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (property[5] == 0)
            {
                property[5]++;
                border1text.Text = "Масштабировать до заполнения";
            }
            else if (property[5] == 1)
            {
                property[5] = 2;
                border1text.Text = "Заполнить";
            }
            else
            {
                property[5] = 0;
                border1text.Text = "Масштабировать";
            }
            txt.Text = "Выполните необходимые настройки";
        }

        private void border2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (property[6] == 0)
            {
                property[6] = 1;
                border2text.Text = "Заливка включена";
            }
            else
            {
                property[6] = 0;
                border2text.Text = "Заливка выключена";
            }
            txt.Text = "Выполните необходимые настройки";
        }

        private void slider0_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            textBlock0.Text = Math.Round(slider0.Value,2).ToString();
            txt.Text = "Выполните необходимые настройки";
        }

        private void tabItemRools_GotFocus(object sender, RoutedEventArgs e)
        {
            txt.Text = "Читайте внимательно";
        }

        private void tabItemAbout_GotFocus(object sender, RoutedEventArgs e)
        {
            txt.Text = "Молодец, что зашел сюда!";
        }

        private void tabItemBack_GotFocus(object sender, RoutedEventArgs e)
        {
            txt.Text = "Выполните необходимые настройки";
        }

        



    }
}
