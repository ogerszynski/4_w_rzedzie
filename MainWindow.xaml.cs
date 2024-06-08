using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace Connect4Client
{
    public partial class MainWindow : Window
    {
        private TcpClient? client = null;
        private NetworkStream? stream = null;
        private int[,] board = new int[6, 7];
        private bool isMyTurn = false;
        private int currentPlayer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeBoard();
            currentPlayer = 1; // Ten klient to gracz 1
        }

        private void InitializeBoard()
        {
            gameCanvas.Children.Clear();
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = 50,
                        Height = 50,
                        Stroke = System.Windows.Media.Brushes.Black,
                        Fill = System.Windows.Media.Brushes.White
                    };
                    Canvas.SetTop(rect, row * 50);
                    Canvas.SetLeft(rect, col * 50);
                    gameCanvas.Children.Add(rect);
                }
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToServerAsync();
        }

        private async Task ConnectToServerAsync()
        {
            string serverIP = serverIPTextBox.Text;
            if (string.IsNullOrEmpty(serverIP))
            {
                statusText.Text = "Please enter a valid server IP.";
                return;
            }

            try
            {
                client = new TcpClient(serverIP, 5000); // Używaj podanego przez użytkownika adresu IP serwera
                stream = client.GetStream();
                statusText.Text = "Connected to server. Waiting for other player...";
                connectButton.IsEnabled = false;

                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                if (message == "start")
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusText.Text = "Both players connected. Game starting...";
                    });
                    // Initialize game board and start game
                }

                await Task.Run(() => ListenForMessages());
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    statusText.Text = "Error connecting to server: " + ex.Message;
                });
                await Task.Delay(5000); // Wait for 5 seconds before retrying
                await ConnectToServerAsync(); // Retry connection
            }
        }

        private async void ListenForMessages()
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                if (stream == null) return; // Dodano sprawdzenie null

                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    if (message.StartsWith("update"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateBoard(message.Substring(7));
                        });
                    }
                    else if (message.StartsWith("win"))
                    {
                        int winner = int.Parse(message.Split(' ')[1]);
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Player {winner} wins!");
                            isMyTurn = false;
                        });
                    }
                    else if (message.StartsWith("turn"))
                    {
                        int playerTurn = int.Parse(message.Split(' ')[1]);
                        Dispatcher.Invoke(() =>
                        {
                            isMyTurn = (playerTurn == currentPlayer);
                            statusText.Text = isMyTurn ? "Your turn" : "Opponent's turn";
                        });
                    }
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusText.Text = "Disconnected from server. Reconnecting...";
                    });
                    await ConnectToServerAsync(); // Retry connection if disconnected
                }
            }
        }

        private void UpdateBoard(string boardState)
        {
            string[] rows = boardState.Split(';');
            for (int row = 0; row < 6; row++)
            {
                string[] cols = rows[row].Split(',');
                for (int col = 0; col < 7; col++)
                {
                    board[row, col] = int.Parse(cols[col]);
                    Rectangle rect = (Rectangle)gameCanvas.Children[row * 7 + col];
                    if (board[row, col] == 1)
                    {
                        rect.Fill = System.Windows.Media.Brushes.Red;
                    }
                    else if (board[row, col] == 2)
                    {
                        rect.Fill = System.Windows.Media.Brushes.Yellow;
                    }
                    else
                    {
                        rect.Fill = System.Windows.Media.Brushes.White;
                    }
                }
            }
        }

        private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMyTurn) return;

            Point mousePosition = e.GetPosition(gameCanvas);
            int col = (int)(mousePosition.X / 50);

            string message = $"move {col}";
            byte[] buffer = Encoding.ASCII.GetBytes(message);

            if (stream == null) return; // Dodano sprawdzenie null

            stream.Write(buffer, 0, buffer.Length);
            isMyTurn = false;
        }
    }
}
