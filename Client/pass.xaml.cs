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
using Serg.WPF;

namespace DixitClient
{
    /// <summary>
    /// Логика взаимодействия для pass.xaml
    /// </summary>
    public partial class pass : SergWindow
    {
        public string pwd;
        rooms r;
        TextBlock txt;
        public pass(rooms _r, string pass)
        {
            InitializeComponent();
            pwd = pass;
            r = _r;
            txt = new TextBlock();
            Tag = txt;
            txt.Text = "Введите пароль!";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (pwd == pwdBox.Text)
            {
                r.passed = true;
                this.Close();
            }
            else
                txt.Text = "Неверный пароль :(";
        }

        private void SergWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Height = 150;
            this.Width = 280;
        }


    }
}
