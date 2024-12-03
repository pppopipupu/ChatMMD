using Mscc.GenerativeAI;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace ChatMMD
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private Thread UI;
        private volatile bool ShouldStop;
        private bool isLoading = false;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            this.Closed += MainWindow_Closed;
            UI = new Thread(UpdateUI);
            ShouldStop = false;
            UI.Start();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            ShouldStop = true;
            UI.Join();
        }

        private void UpdateUI()
        {
            while (!ShouldStop)
            {
                this.Title_XD.Dispatcher.Invoke(new Action(() =>
                {
                    Random random = new Random();
                    this.Title_XD.Foreground = new LinearGradientBrush(Color.FromRgb((byte)random.Next(128, 255),
                        (byte)random.Next(128, 255), (byte)random.Next(128, 255)),
                        Color.FromRgb((byte)random.Next(128, 255), (byte)random.Next(128, 255), (byte)random.Next(128, 255)), 45);
                }));
                Thread.Sleep(200);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            isLoading = true;
            List<string> list = new List<string>();
            foreach (object s in API_LIST.Items)
            {
                list.Add(s.ToString());
            }
            if (list.Count == 0)
            {
                isLoading = false;
                return;
            }
            Tip.Text = "测试api中";

            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    var googleAI = new GoogleAI(apiKey: API_LIST.Items[i].ToString());
                    var model = googleAI.GenerativeModel(model: Model.Gemini15Flash002);

                    WebProxy webProxy = new WebProxy(proxy.Text);
                    WebRequest.DefaultWebProxy = webProxy;

                    var response = await model.GenerateContent("你好");
                    if (response.Text != null)
                    {
                        Tip.Text = "通过，加载中";
                        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/"))
                        {
                            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/");
                        }
                        StreamWriter sw = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\motion.json");

                        sw.Write(@"{ ""Header"": { ""FileSignature"": ""Vocaloid Motion Data 0002"", ""ModelName"": ""MMD"" }, ""Motion"": { ""Count"": 0, ""Data"": [ ] }, ""Skin"": { ""Count"": 0, ""Data"": [ ] }, ""Camera"": { ""Count"": 0, ""Data"": [ ] }, ""Illumination"": { ""Count"": 0, ""Data"": [ ] }, ""SelfShadow"": { ""Count"": 0, ""Data"": [ ] }, ""IK"": { ""Count"": 0, ""Data"": [ ] }, ""Expansion"": { ""TargetID"": -1, ""StartFrame"": 0, ""Version"": 2, ""FileType"": ""VMD"", ""CoordinateSystem"": ""LeftHand"" } }");
                        sw.Close();
                        Chat chat = new Chat();
                        chat.api_keys = list;                
                        this.Close();
                        Thread.Sleep(500);
                       Process.Start("SabaViewer.exe");
                        chat.Show();

                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (i == list.Count - 1)
                    {
                   
                        Tip.Text = "失败，请检查网络环境和api key";
                        isLoading = false;
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            proxy.Text = "127.0.0.1:7890";
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (API_KEY.Text != string.Empty)
            {
                API_LIST.Items.Add(API_KEY.Text);
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            try
            {
                API_LIST.Items.RemoveAt(API_LIST.Items.Count - 1);
            }
            catch (Exception ex)
            {
                Tip.Text = "没有api，删牛魔";
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\chat.json");
        }
    }
}