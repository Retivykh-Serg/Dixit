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

namespace DixitClient
{
    /// <summary>
    /// Логика взаимодействия для messageViewer.xaml
    /// </summary>
    public partial class messageViewer : Window
    {
        int logSize = 100;
        int currentSize = 0;

        public messageViewer()
        {
            InitializeComponent();
        }

        public void SetLogin(string login)
        {
            this.Title += " " + login;
        }

        public void AddMsg(string message, bool isRecieved)
        {
            if (currentSize >= logSize)
            {
                log.Inlines.Remove(log.Inlines.FirstInline); //удалили время
                log.Inlines.Remove(log.Inlines.FirstInline); //удалили текст
                currentSize--;
            }
            string time = "[" + System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "] ";
            Span DateSpan = new Span();
            DateSpan.Inlines.Add(time);
            DateSpan.Foreground = Brushes.DarkGreen;
            DateSpan.FontWeight = FontWeights.Bold;
            log.Inlines.Add(DateSpan);

            Span Msg = new Span();
            Msg.Inlines.Add(message);
            if (isRecieved)
                Msg.Foreground = Brushes.OrangeRed;
            else
                Msg.Foreground = Brushes.DodgerBlue;
            
            log.Inlines.Add(Msg);
            scrollViewer.UpdateLayout();
            scrollViewer.ScrollToEnd();
            currentSize++;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            string Path = "Commands " + System.DateTime.Now.Date.ToShortDateString() + " " + System.DateTime.Now.TimeOfDay.ToString(@"hh\-mm") + ".txt";
            StreamWriter objWriter = new StreamWriter(@Path);
            string[] str = log.Text.Split('\n');
            foreach (string s in str)
                objWriter.WriteLine(s);
            objWriter.Close();
            objWriter.Dispose();
        }
    }
}
