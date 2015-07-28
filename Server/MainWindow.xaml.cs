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

namespace DixitServer
{
        
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Server serv;

        public MainWindow()
        {
            serv = new Server();

            InitializeComponent();

            serv.logChanged += serv_logChanged;
            serv.playersChanged += serv_playersChanged;
            serv.roomsChanged += serv_roomsChanged;
            serv.log2Changed += new EventHandler(serv_log2Changed);

            logging.DataContext = serv;
        }

        void serv_log2Changed(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                logging2.Text = serv.log2;
            }));
        }

        void serv_roomsChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                roomsDataGrid.Items.Clear();
                for (int i = 0; i < serv.rooms.Count; i++)
                    roomsDataGrid.Items.Add(serv.rooms[i]);
                serv.sendNewListsOfRooms();
            }));
        }

        void serv_playersChanged(object sender, EventArgs e) 
        {
            Dispatcher.Invoke((Action)(() =>
            {
                usersDataGrid.Items.Clear();
                for (int i = 0; i < serv.clientList.Count; i++)
                    usersDataGrid.Items.Add(serv.clientList[i]);
                if ((bool)sender == true) serv.sendNewListOfPlayers();
            }));
        }

        private void serv_logChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() => { logging.Text = serv.log; }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                serv.SendServerShutdown();
            }));
        }

        private void checkBoxSave1_Unchecked(object sender, RoutedEventArgs e)
        {
            serv.saveLog1 = false;
        }

        private void checkBoxSave1_Checked(object sender, RoutedEventArgs e)
        {
            serv.saveLog1 = true;
        }

        private void checkBoxSave2_Checked(object sender, RoutedEventArgs e)
        {
            serv.saveLog2 = true;
        }

        private void checkBoxSave2_Unchecked(object sender, RoutedEventArgs e)
        {
            serv.saveLog1 = false;
        }

    }

}
