using Microsoft.Win32;
using Mscc.GenerativeAI;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace ChatMMD
{
    /// <summary>
    /// Chat.xaml 的交互逻辑
    /// </summary>
    public partial class Chat
    {
        public ObservableCollection<Message> Messages { get; set; } = new ObservableCollection<Message>();
        public List<ContentResponse> History { get; set; } = new List<ContentResponse>();
        public List<string> api_keys { get; set; }
        public Process Saba { get; set; }
        //private List<SafetySetting> safetySetting = new List<SafetySetting>()
        //{
        //    new SafetySetting(){Category = HarmCategory.HarmCategoryDangerousContent,Threshold = HarmBlockThreshold.BlockNone},
        //      new SafetySetting(){Category = HarmCategory.HarmCategoryHarassment,Threshold = HarmBlockThreshold.BlockNone},
        //        new SafetySetting(){Category = HarmCategory.HarmCategorySexuallyExplicit,Threshold = HarmBlockThreshold.BlockNone},
        //          new SafetySetting(){Category = HarmCategory.HarmCategoryUnspecified,Threshold = HarmBlockThreshold.BlockNone},
        //            new SafetySetting(){Category = HarmCategory.HarmCategoryCivicIntegrity,Threshold = HarmBlockThreshold.BlockNone},
        //              new SafetySetting(){Category = HarmCategory.HarmCategoryHateSpeech,Threshold = HarmBlockThreshold.BlockNone}

        //};
        private bool isLoading = false;

        public Chat()
        {
            InitializeComponent();
            DataContext = this;
            Closed += Chat_Closed;
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\chat.json"))
            {
                History = System.Text.Json.JsonSerializer.Deserialize<List<ContentResponse>>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\chat.json"));
            }
        }

        private void Chat_Closed(object? sender, EventArgs e)
        {
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/"))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/");
            }
            StreamWriter sw = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\chat.json");
            JArray ja = JArray.Parse(System.Text.Json.JsonSerializer.Serialize(History));

            JArray modifiedJa = new JArray();

            foreach (JObject item in ja)
            {
                JArray modifiedParts = new JArray();
                foreach (JObject part in item["Parts"])
                {
                    if (part["InlineData"].Type== JTokenType.Null)
                    {
                        modifiedParts.Add(part);
                    }
                }

                JObject modifiedItem = new JObject(item);
                modifiedItem["Parts"] = modifiedParts;
                modifiedJa.Add(modifiedItem);
            }
            sw.Write(modifiedJa.ToString());
            sw.Close();
            Saba.Kill();
            Environment.Exit(0);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fileName);
                        bitmap.EndInit();

                        ImageView.Items.Add(fileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载图片时遇到错误: {fileName}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Image_Remove(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Controls.Image image = sender as System.Windows.Controls.Image;
            if (image != null)
            {
                string imagePath = image.DataContext as string;

                for (int i = 0; i < ImageView.Items.Count; i++)
                {
                    if (ImageView.Items[i].ToString() == imagePath)
                    {
                        ImageView.Items.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void Audio_Remove(object sender, MouseButtonEventArgs e)
        {
            TextBlock text = sender as TextBlock;
            if (text != null)
            {
                string audioPath = text.Text;

                for (int i = 0; i < AudioView.Items.Count; i++)
                {
                    if (AudioView.Items[i].ToString() == audioPath)
                    {
                        AudioView.Items.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio files (*.mp3;*.ogg;*.wav;)|*.mp3;*.ogg;*.wav;|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    AudioView.Items.Add(fileName);
                }
            }
        }

        private async void SendMessage_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;
            if (TextPrompt.Text == string.Empty && ImageView.Items.Count == 0 && AudioView.Items.Count == 0)
                return;
            isLoading = true;
            SendMessage.Content = "正在回复";
            List<string> images = new List<string>();
            List<string> audios = new List<string>();
            foreach (var item in AudioView.Items) { audios.Add((string)item); }
            foreach (var item in ImageView.Items) { images.Add((string)item); }
            Messages.Add(new Message() { _Message = "User:" + TextPrompt.Text, Images = images, Audios = audios });

            for (int i = 0; i < api_keys.Count; i++)
            {
                try
                {
                    var googleAI = new GoogleAI(apiKey: api_keys[i].ToString());

                    var model = googleAI.GenerativeModel(model: Model.Gemini15Flash002, systemInstruction: new Content("你将扮演一个MMD模型，每次输出都必须包含response部分和vmd动作部分，response是你说的话，vmd是你做出的动作，所以你的最终输出json格式应为{response:\"你说的话\",vmd:\"动作json\"},并且请务必保证json格式正确,每次回答你都会对对方说的话做出不同的的动作,vmd动作中所有骨骼名的英文字母和阿拉伯数字为全角,并使用json编写vmd动作，请尽量延长你的动作并使动作符合你说的话,骨骼名均为标准MMD日文骨骼。你不应使用不在骨骼列表里的骨骼，且至少创建两个相同骨骼的关键帧以使骨骼运动，操控腿时移动右足ＩＫ/左足ＩＫ，因为IK意为Inverse Kinematics（反向运动），其他IK骨也是这么使用的,骨骼列表说明如下：" +
                        " センター  center 中心骨\r\n\r\n上半身 用于整个模型中控制上半身的骨骼\r\n\r\n首 脖子\r\n\r\n頭 头\r\n\r\n両目 同时控制左右眼，使它们同时动的，是左目/右目的付与亲\r\n\r\n左目 左眼\r\n\r\n右目 右眼\r\n\r\n下半身 用于整个模型中控制下半身的骨骼\r\n\r\n右肩/左肩 肩膀\r\n\r\n右腕/左腕 手臂\r\n\r\n右ひじ/左ひじ  手小臂，也就是肘\r\n\r\n右手首/左手首 手掌\r\n\r\n右親指１、右親指２/左親指１、左親指２ 拇指，数字为全角数字\r\n\r\n右人指１、右人指２、右人指３/左人指１、左人指２、左人指３ 食指，数字为全角数字\r\n\r\n右中指１、右中指２、右中指３/左中指１、左中指２、左中指３ 中指，数字为全角数字\r\n\r\n右薬指１、右薬指２、右薬指３/左薬指１、左薬指２、左薬指３ 无名指，数字为全角数字\r\n\r\n右小指１、右小指２、右小指３/左小指１、左小指２、左小指３ 小指，数字为全角数字\r\n\r\n右足/左足 大腿\r\n\r\n右ひざ/左ひざ 膝盖，小腿\r\n\r\n右足首/左足首 脚掌，指向脚尖骨\r\n\r\n右つま先/左つま先 脚尖，但是本身是个隐藏骨，且上面没有权重\r\n\r\n右足ＩＫ/左足ＩＫ 影响腿的骨骼\r\n\r\n右つま先ＩＫ/左つま先ＩＫ 影响脚掌的骨骼\r\n\r\n全ての親 用于模型整体移动\r\n\r\nグルーブ groove，跟center骨用处一样，可以当重心来用\r\n\r\n上半身2 亲骨是上半身，上半身的骨中偏上的一根\r\n\r\n右腕捩/左腕捩 控制手大臂绕臂旋转\r\n\r\n右ひじ捩/左ひじ捩 控制小臂绕臂旋转\r\n\r\n右肩P/左肩P 用来耸肩的骨骼\r\n\r\n右肩C/左肩C 用来协助耸肩的骨骼\r\n\r\n右親指０/左親指０ 特殊动作可能会用到的大拇指的骨\r\n\r\n腰 就是腰\r\n\r\n右足IK親/左足IK親 一般在左/右足首骨下面，一个单纯的移动骨骼，亲骨是全亲骨，作为足IK的亲骨骼\r\n\r\n腰キャンセル右/腰キャンセル左 腰取消骨，用于保持脚部位置不变的时候外翻膝盖用的\r\n\r\n右足D/左足D 同足，用于脱离足ik限制进行踢脚使用的骨\r\n\r\n右ひざD/左ひざD 同ひざ，用于脱离足ik限制进行踢脚使用的骨\r\n\r\n右足首D/左足首D 同足首，用于脱离足ik限制进行踢脚使用的骨\r\n\r\n右足先EX/左足先EX 前脚掌(骨骼说明结束）"
                        + "json动画格式如下：{\r\n  \"Header\": {\r\n      \"FileSignature\": \"Vocaloid Motion Data 0002\",\r\n      \"ModelName\": \"Model\"\r\n  },\r\n  \"Motion\": {\r\n    \"Count\": 5,\r\n    \"Data\": [\r\n        {\r\n        \"FrameNo\": 0,\r\n        \"Name\": \"左ひじ\",\r\n        \"Location\": [0, 0, 0],\r\n        \"Rotation\": {\r\n          \"Quaternion\": [1, 0, 0, 0],\r\n          \"Euler\": [-0, 0, 0]\r\n        },\r\n        \"Interpolation\": {\r\n          \"X\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Y\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Z\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Rotation\": {\"start\":[20, 20], \"end\":[107, 107]}\r\n        }\r\n      },\r\n        {\r\n        \"FrameNo\": 19,\r\n        \"Name\": \"左ひじ\",\r\n        \"Location\": [0, 0, 0],\r\n        \"Rotation\": {\r\n          \"Quaternion\": [-0.55723965, -0.7626573, 0.21307592, 0.24987298],\r\n          \"Euler\": [8.3, 37.6, 104.9]\r\n        },\r\n        \"Interpolation\": {\r\n          \"X\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Y\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Z\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Rotation\": {\"start\":[42, 0], \"end\":[85, 127]}\r\n        }\r\n      },\r\n        {\r\n        \"FrameNo\": 0,\r\n        \"Name\": \"右足ＩＫ\",\r\n        \"Location\": [0, 0, 0],\r\n        \"Rotation\": {\r\n          \"Quaternion\": [1, 0, 0, 0],\r\n          \"Euler\": [-0, 0, 0]\r\n        },\r\n        \"Interpolation\": {\r\n          \"X\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Y\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Z\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Rotation\": {\"start\":[20, 20], \"end\":[107, 107]}\r\n        }\r\n      },\r\n        {\r\n        \"FrameNo\": 3,\r\n        \"Name\": \"右足ＩＫ\",\r\n        \"Location\": [0, 0, 0],\r\n        \"Rotation\": {\r\n          \"Quaternion\": [1, 0, 0, 0],\r\n          \"Euler\": [-0, 0, 0]\r\n        },\r\n        \"Interpolation\": {\r\n          \"X\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Y\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Z\": {\"start\":[20, 20], \"end\":[107, 107]},\r\n          \"Rotation\": {\"start\":[20, 20], \"end\":[107, 107]}\r\n        }\r\n      },\r\n        {\r\n        \"FrameNo\": 14,\r\n        \"Name\": \"右足ＩＫ\",\r\n        \"Location\": [-18.56, 28.23, -23.07],\r\n        \"Rotation\": {\r\n          \"Quaternion\": [1, 0, 0, 0],\r\n          \"Euler\": [-0, 0, 0]\r\n        },\r\n        \"Interpolation\": {\r\n          \"X\": {\"start\":[42, 0], \"end\":[85, 127]},\r\n          \"Y\": {\"start\":[42, 0], \"end\":[85, 127]},\r\n          \"Z\": {\"start\":[42, 0], \"end\":[85, 127]},\r\n          \"Rotation\": {\"start\":[20, 20], \"end\":[107, 107]}\r\n        }\r\n      }\r\n    ]\r\n  },\r\n  \"Skin\": {\r\n    \"Count\": 0,\r\n    \"Data\": [\r\n    ]\r\n  },\r\n  \"Camera\": {\r\n    \"Count\": 0,\r\n    \"Data\": [\r\n    ]\r\n  },\r\n  \"Illumination\": {\r\n    \"Count\": 0,\r\n    \"Data\": [\r\n    ]\r\n  },\r\n  \"SelfShadow\": {\r\n    \"Count\": 0,\r\n    \"Data\": [\r\n    ]\r\n  },\r\n  \"IK\": {\r\n    \"Count\": 0,\r\n    \"Data\": [\r\n    ]\r\n  },\r\n  \"Expansion\": {\r\n      \"TargetID\": -1,\r\n      \"StartFrame\": 0,\r\n      \"Version\": 2,\r\n      \"FileType\": \"VMD\",\r\n      \"CoordinateSystem\": \"LeftHand\"\r\n  }\r\n}(json动画示例结束）" + "这是一个让角色踢腿并抬起左手的动画，你的动画必须符合这个格式"));
                    model.UseJsonMode = true;

                    var chat = model.StartChat(History, generationConfig: new GenerationConfig()
                    {
                        Temperature = (float)Temperature.Value,
                        TopP = (float)Top_P.Value,
                        EnableEnhancedCivicAnswers = false,

                        ResponseMimeType = "application/json"
                    });
                    List<Part> parts = new List<Part>();
                    foreach (var media in images.Concat(audios))
                    {
                        parts.Add(new Part() { InlineData = new InlineData { MimeType = GetMimeType(media), Data = Convert.ToBase64String(await File.ReadAllBytesAsync(media)) } });
                    }

                    parts.Add(new Part() { Text = TextPrompt.Text });

                    var response = await chat.SendMessage(parts);
                    if (response.Text != null)
                    {
                        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/"))
                        {
                            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/ChatMMD/");
                        }
                        StreamWriter sw = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ChatMMD\motion.json");
                        JObject jO = JObject.Parse(response.Text);
                        sw.Write(jO["vmd"].ToString());
                        sw.Close();
                        Messages.Add(new Message() { _Message = "Model:" + jO["response"].ToString() });
                        isLoading = false;
                        SendMessage.Content = "发送";
                        TextPrompt.Text = string.Empty;
                        ImageView.Items.Clear();
                        AudioView.Items.Clear();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (i < api_keys.Count - 1)
                    {
                     
                        continue;
                    }
                    else
                    {
                     
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
        }

        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string audioPath)
            {
                var parent = button.Parent as StackPanel;
                if (parent != null)
                {
                    var mediaElement = parent.Children.OfType<MediaElement>().FirstOrDefault();

                    if (mediaElement != null)
                    {
                        try
                        {
                            mediaElement.Source = new Uri(audioPath, UriKind.RelativeOrAbsolute);
                            mediaElement.Play();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"音频播放出错: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static string GetMimeType(string uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            string text = Path.GetExtension(uri).ToLower();
            if (text.StartsWith("."))
            {
                text = text.Substring(1);
            }

            return text switch
            {
                "323" => "text/h323",
                "3g2" => "video/3gpp2",
                "3gp" => "video/3gpp",
                "3gp2" => "video/3gpp2",
                "3gpp" => "video/3gpp",
                "7z" => "application/x-7z-compressed",
                "aa" => "audio/audible",
                "aac" => "audio/aac",
                "aaf" => "application/octet-stream",
                "aax" => "audio/vnd.audible.aax",
                "ac3" => "audio/ac3",
                "aca" => "application/octet-stream",
                "accda" => "application/msaccess.addin",
                "accdb" => "application/msaccess",
                "accdc" => "application/msaccess.cab",
                "accde" => "application/msaccess",
                "accdr" => "application/msaccess.runtime",
                "accdt" => "application/msaccess",
                "accdw" => "application/msaccess.webapplication",
                "accft" => "application/msaccess.ftemplate",
                "acx" => "application/internet-property-stream",
                "addin" => "text/xml",
                "ade" => "application/msaccess",
                "adobebridge" => "application/x-bridge-url",
                "adp" => "application/msaccess",
                "adt" => "audio/vnd.dlna.adts",
                "adts" => "audio/aac",
                "afm" => "application/octet-stream",
                "ai" => "application/postscript",
                "aif" => "audio/x-aiff",
                "aifc" => "audio/aiff",
                "aiff" => "audio/aiff",
                "air" => "application/vnd.adobe.air-application-installer-package+zip",
                "amc" => "application/x-mpeg",
                "application" => "application/x-ms-application",
                "art" => "image/x-jg",
                "asa" => "application/xml",
                "asax" => "application/xml",
                "ascx" => "application/xml",
                "asd" => "application/octet-stream",
                "asf" => "video/x-ms-asf",
                "ashx" => "application/xml",
                "asi" => "application/octet-stream",
                "asm" => "text/plain",
                "asmx" => "application/xml",
                "aspx" => "application/xml",
                "asr" => "video/x-ms-asf",
                "asx" => "video/x-ms-asf",
                "atom" => "application/atom+xml",
                "au" => "audio/basic",
                "avi" => "video/x-msvideo",
                "axs" => "application/olescript",
                "bas" => "text/plain",
                "bcpio" => "application/x-bcpio",
                "bin" => "application/octet-stream",
                "bmp" => "image/bmp",
                "c" => "text/plain",
                "cab" => "application/octet-stream",
                "caf" => "audio/x-caf",
                "calx" => "application/vnd.ms-office.calx",
                "cat" => "application/vnd.ms-pki.seccat",
                "cc" => "text/plain",
                "cd" => "text/plain",
                "cdda" => "audio/aiff",
                "cdf" => "application/x-cdf",
                "cer" => "application/x-x509-ca-cert",
                "chm" => "application/octet-stream",
                "class" => "application/x-java-applet",
                "clp" => "application/x-msclip",
                "cmx" => "image/x-cmx",
                "cnf" => "text/plain",
                "cod" => "image/cis-cod",
                "config" => "application/xml",
                "contact" => "text/x-ms-contact",
                "coverage" => "application/xml",
                "cpio" => "application/x-cpio",
                "cpp" => "text/plain",
                "crd" => "application/x-mscardfile",
                "crl" => "application/pkix-crl",
                "crt" => "application/x-x509-ca-cert",
                "cs" => "text/plain",
                "csdproj" => "text/plain",
                "csh" => "application/x-csh",
                "csproj" => "text/plain",
                "css" => "text/css",
                "csv" => "text/csv",
                "cur" => "application/octet-stream",
                "cxx" => "text/plain",
                "dat" => "application/octet-stream",
                "datasource" => "application/xml",
                "dbproj" => "text/plain",
                "dcr" => "application/x-director",
                "def" => "text/plain",
                "deploy" => "application/octet-stream",
                "der" => "application/x-x509-ca-cert",
                "dgml" => "application/xml",
                "dib" => "image/bmp",
                "dif" => "video/x-dv",
                "dir" => "application/x-director",
                "disco" => "text/xml",
                "dll" => "application/x-msdownload",
                "dll.config" => "text/xml",
                "dlm" => "text/dlm",
                "doc" => "application/msword",
                "docm" => "application/vnd.ms-word.document.macroenabled.12",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "dot" => "application/msword",
                "dotm" => "application/vnd.ms-word.template.macroenabled.12",
                "dotx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
                "dsp" => "application/octet-stream",
                "dsw" => "text/plain",
                "dtd" => "text/xml",
                "dtsconfig" => "text/xml",
                "dv" => "video/x-dv",
                "dvi" => "application/x-dvi",
                "dwf" => "drawing/x-dwf",
                "dwp" => "application/octet-stream",
                "dxr" => "application/x-director",
                "eml" => "message/rfc822",
                "emz" => "application/octet-stream",
                "eot" => "application/octet-stream",
                "eps" => "application/postscript",
                "etl" => "application/etl",
                "etx" => "text/x-setext",
                "evy" => "application/envoy",
                "exe" => "application/octet-stream",
                "exe.config" => "text/xml",
                "fdf" => "application/vnd.fdf",
                "fif" => "application/fractals",
                "filters" => "application/xml",
                "fla" => "application/octet-stream",
                "flr" => "x-world/x-vrml",
                "flv" => "video/x-flv",
                "fsscript" => "application/fsharp-script",
                "fsx" => "application/fsharp-script",
                "generictest" => "application/xml",
                "gif" => "image/gif",
                "group" => "text/x-ms-group",
                "gsm" => "audio/x-gsm",
                "gtar" => "application/x-gtar",
                "gz" => "application/x-gzip",
                "h" => "text/plain",
                "hdf" => "application/x-hdf",
                "hdml" => "text/x-hdml",
                "hhc" => "application/x-oleobject",
                "hhk" => "application/octet-stream",
                "hhp" => "application/octet-stream",
                "hlp" => "application/winhlp",
                "hpp" => "text/plain",
                "hqx" => "application/mac-binhex40",
                "hta" => "application/hta",
                "htc" => "text/x-component",
                "htm" => "text/html",
                "html" => "text/html",
                "htt" => "text/webviewhtml",
                "hxa" => "application/xml",
                "hxc" => "application/xml",
                "hxd" => "application/octet-stream",
                "hxe" => "application/xml",
                "hxf" => "application/xml",
                "hxh" => "application/octet-stream",
                "hxi" => "application/octet-stream",
                "hxk" => "application/xml",
                "hxq" => "application/octet-stream",
                "hxr" => "application/octet-stream",
                "hxs" => "application/octet-stream",
                "hxt" => "text/html",
                "hxv" => "application/xml",
                "hxw" => "application/octet-stream",
                "hxx" => "text/plain",
                "i" => "text/plain",
                "ico" => "image/x-icon",
                "ics" => "application/octet-stream",
                "idl" => "text/plain",
                "ief" => "image/ief",
                "iii" => "application/x-iphone",
                "inc" => "text/plain",
                "inf" => "application/octet-stream",
                "inl" => "text/plain",
                "ins" => "application/x-internet-signup",
                "ipa" => "application/x-itunes-ipa",
                "ipg" => "application/x-itunes-ipg",
                "ipproj" => "text/plain",
                "ipsw" => "application/x-itunes-ipsw",
                "iqy" => "text/x-ms-iqy",
                "isp" => "application/x-internet-signup",
                "ite" => "application/x-itunes-ite",
                "itlp" => "application/x-itunes-itlp",
                "itms" => "application/x-itunes-itms",
                "itpc" => "application/x-itunes-itpc",
                "ivf" => "video/x-ivf",
                "jar" => "application/java-archive",
                "java" => "application/octet-stream",
                "jck" => "application/liquidmotion",
                "jcz" => "application/liquidmotion",
                "jfif" => "image/pjpeg",
                "jnlp" => "application/x-java-jnlp-file",
                "jpb" => "application/octet-stream",
                "jpe" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "jpg" => "image/jpeg",
                "js" => "application/x-javascript",
                "jsx" => "text/jscript",
                "jsxbin" => "text/plain",
                "latex" => "application/x-latex",
                "library-ms" => "application/windows-library+xml",
                "lit" => "application/x-ms-reader",
                "loadtest" => "application/xml",
                "lpk" => "application/octet-stream",
                "lsf" => "video/x-la-asf",
                "lst" => "text/plain",
                "lsx" => "video/x-la-asf",
                "lzh" => "application/octet-stream",
                "m13" => "application/x-msmediaview",
                "m14" => "application/x-msmediaview",
                "m1v" => "video/mpeg",
                "m2t" => "video/vnd.dlna.mpeg-tts",
                "m2ts" => "video/vnd.dlna.mpeg-tts",
                "m2v" => "video/mpeg",
                "m3u" => "audio/x-mpegurl",
                "m3u8" => "audio/x-mpegurl",
                "m4a" => "audio/m4a",
                "m4b" => "audio/m4b",
                "m4p" => "audio/m4p",
                "m4r" => "audio/x-m4r",
                "m4v" => "video/x-m4v",
                "mac" => "image/x-macpaint",
                "mak" => "text/plain",
                "man" => "application/x-troff-man",
                "manifest" => "application/x-ms-manifest",
                "map" => "text/plain",
                "master" => "application/xml",
                "mda" => "application/msaccess",
                "mdb" => "application/x-msaccess",
                "mde" => "application/msaccess",
                "mdp" => "application/octet-stream",
                "me" => "application/x-troff-me",
                "mfp" => "application/x-shockwave-flash",
                "mht" => "message/rfc822",
                "mhtml" => "message/rfc822",
                "mid" => "audio/mid",
                "midi" => "audio/mid",
                "mix" => "application/octet-stream",
                "mk" => "text/plain",
                "mmf" => "application/x-smaf",
                "mno" => "text/xml",
                "mny" => "application/x-msmoney",
                "mod" => "video/mpeg",
                "mov" => "video/quicktime",
                "movie" => "video/x-sgi-movie",
                "mp2" => "video/mpeg",
                "mp2v" => "video/mpeg",
                "mp3" => "audio/mpeg",
                "mp4" => "video/mp4",
                "mp4v" => "video/mp4",
                "mpa" => "video/mpeg",
                "mpe" => "video/mpeg",
                "mpeg" => "video/mpeg",
                "mpf" => "application/vnd.ms-mediapackage",
                "mpg" => "video/mpeg",
                "mpp" => "application/vnd.ms-project",
                "mpv2" => "video/mpeg",
                "mqv" => "video/quicktime",
                "ms" => "application/x-troff-ms",
                "msi" => "application/octet-stream",
                "mso" => "application/octet-stream",
                "mts" => "video/vnd.dlna.mpeg-tts",
                "mtx" => "application/xml",
                "mvb" => "application/x-msmediaview",
                "mvc" => "application/x-miva-compiled",
                "mxp" => "application/x-mmxp",
                "nc" => "application/x-netcdf",
                "nsc" => "video/x-ms-asf",
                "nws" => "message/rfc822",
                "ocx" => "application/octet-stream",
                "oda" => "application/oda",
                "odc" => "text/x-ms-odc",
                "odh" => "text/plain",
                "odl" => "text/plain",
                "odp" => "application/vnd.oasis.opendocument.presentation",
                "ods" => "application/oleobject",
                "odt" => "application/vnd.oasis.opendocument.text",
                "one" => "application/onenote",
                "onea" => "application/onenote",
                "onepkg" => "application/onenote",
                "onetmp" => "application/onenote",
                "onetoc" => "application/onenote",
                "onetoc2" => "application/onenote",
                "orderedtest" => "application/xml",
                "osdx" => "application/opensearchdescription+xml",
                "p10" => "application/pkcs10",
                "p12" => "application/x-pkcs12",
                "p7b" => "application/x-pkcs7-certificates",
                "p7c" => "application/pkcs7-mime",
                "p7m" => "application/pkcs7-mime",
                "p7r" => "application/x-pkcs7-certreqresp",
                "p7s" => "application/pkcs7-signature",
                "pbm" => "image/x-portable-bitmap",
                "pcast" => "application/x-podcast",
                "pct" => "image/pict",
                "pcx" => "application/octet-stream",
                "pcz" => "application/octet-stream",
                "pdf" => "application/pdf",
                "pfb" => "application/octet-stream",
                "pfm" => "application/octet-stream",
                "pfx" => "application/x-pkcs12",
                "pgm" => "image/x-portable-graymap",
                "pic" => "image/pict",
                "pict" => "image/pict",
                "pkgdef" => "text/plain",
                "pkgundef" => "text/plain",
                "pko" => "application/vnd.ms-pki.pko",
                "pls" => "audio/scpls",
                "pma" => "application/x-perfmon",
                "pmc" => "application/x-perfmon",
                "pml" => "application/x-perfmon",
                "pmr" => "application/x-perfmon",
                "pmw" => "application/x-perfmon",
                "png" => "image/png",
                "pnm" => "image/x-portable-anymap",
                "pnt" => "image/x-macpaint",
                "pntg" => "image/x-macpaint",
                "pnz" => "image/png",
                "pot" => "application/vnd.ms-powerpoint",
                "potm" => "application/vnd.ms-powerpoint.template.macroenabled.12",
                "potx" => "application/vnd.openxmlformats-officedocument.presentationml.template",
                "ppa" => "application/vnd.ms-powerpoint",
                "ppam" => "application/vnd.ms-powerpoint.addin.macroenabled.12",
                "ppm" => "image/x-portable-pixmap",
                "pps" => "application/vnd.ms-powerpoint",
                "ppsm" => "application/vnd.ms-powerpoint.slideshow.macroenabled.12",
                "ppsx" => "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
                "ppt" => "application/vnd.ms-powerpoint",
                "pptm" => "application/vnd.ms-powerpoint.presentation.macroenabled.12",
                "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "prf" => "application/pics-rules",
                "prm" => "application/octet-stream",
                "prx" => "application/octet-stream",
                "ps" => "application/postscript",
                "psc1" => "application/powershell",
                "psd" => "application/octet-stream",
                "psess" => "application/xml",
                "psm" => "application/octet-stream",
                "psp" => "application/octet-stream",
                "pub" => "application/x-mspublisher",
                "pwz" => "application/vnd.ms-powerpoint",
                "qht" => "text/x-html-insertion",
                "qhtm" => "text/x-html-insertion",
                "qt" => "video/quicktime",
                "qti" => "image/x-quicktime",
                "qtif" => "image/x-quicktime",
                "qtl" => "application/x-quicktimeplayer",
                "qxd" => "application/octet-stream",
                "ra" => "audio/x-pn-realaudio",
                "ram" => "audio/x-pn-realaudio",
                "rar" => "application/octet-stream",
                "ras" => "image/x-cmu-raster",
                "rat" => "application/rat-file",
                "rc" => "text/plain",
                "rc2" => "text/plain",
                "rct" => "text/plain",
                "rdlc" => "application/xml",
                "resx" => "application/xml",
                "rf" => "image/vnd.rn-realflash",
                "rgb" => "image/x-rgb",
                "rgs" => "text/plain",
                "rm" => "application/vnd.rn-realmedia",
                "rmi" => "audio/mid",
                "rmp" => "application/vnd.rn-rn_music_package",
                "roff" => "application/x-troff",
                "rpm" => "audio/x-pn-realaudio-plugin",
                "rqy" => "text/x-ms-rqy",
                "rtf" => "application/rtf",
                "rtx" => "text/richtext",
                "ruleset" => "application/xml",
                "s" => "text/plain",
                "safariextz" => "application/x-safari-safariextz",
                "scd" => "application/x-msschedule",
                "sct" => "text/scriptlet",
                "sd2" => "audio/x-sd2",
                "sdp" => "application/sdp",
                "sea" => "application/octet-stream",
                "searchconnector-ms" => "application/windows-search-connector+xml",
                "setpay" => "application/set-payment-initiation",
                "setreg" => "application/set-registration-initiation",
                "settings" => "application/xml",
                "sgimb" => "application/x-sgimb",
                "sgml" => "text/sgml",
                "sh" => "application/x-sh",
                "shar" => "application/x-shar",
                "shtml" => "text/html",
                "sit" => "application/x-stuffit",
                "sitemap" => "application/xml",
                "skin" => "application/xml",
                "sldm" => "application/vnd.ms-powerpoint.slide.macroenabled.12",
                "sldx" => "application/vnd.openxmlformats-officedocument.presentationml.slide",
                "slk" => "application/vnd.ms-excel",
                "sln" => "text/plain",
                "slupkg-ms" => "application/x-ms-license",
                "smd" => "audio/x-smd",
                "smi" => "application/octet-stream",
                "smx" => "audio/x-smd",
                "smz" => "audio/x-smd",
                "snd" => "audio/basic",
                "snippet" => "application/xml",
                "snp" => "application/octet-stream",
                "sol" => "text/plain",
                "sor" => "text/plain",
                "spc" => "application/x-pkcs7-certificates",
                "spl" => "application/futuresplash",
                "src" => "application/x-wais-source",
                "srf" => "text/plain",
                "ssisdeploymentmanifest" => "text/xml",
                "ssm" => "application/streamingmedia",
                "sst" => "application/vnd.ms-pki.certstore",
                "stl" => "application/vnd.ms-pki.stl",
                "sv4cpio" => "application/x-sv4cpio",
                "sv4crc" => "application/x-sv4crc",
                "svc" => "application/xml",
                "swf" => "application/x-shockwave-flash",
                "t" => "application/x-troff",
                "tar" => "application/x-tar",
                "tcl" => "application/x-tcl",
                "testrunconfig" => "application/xml",
                "testsettings" => "application/xml",
                "tex" => "application/x-tex",
                "texi" => "application/x-texinfo",
                "texinfo" => "application/x-texinfo",
                "tgz" => "application/x-compressed",
                "thmx" => "application/vnd.ms-officetheme",
                "thn" => "application/octet-stream",
                "tif" => "image/tiff",
                "tiff" => "image/tiff",
                "tlh" => "text/plain",
                "tli" => "text/plain",
                "toc" => "application/octet-stream",
                "tr" => "application/x-troff",
                "trm" => "application/x-msterminal",
                "trx" => "application/xml",
                "ts" => "video/vnd.dlna.mpeg-tts",
                "tsv" => "text/tab-separated-values",
                "ttf" => "application/octet-stream",
                "tts" => "video/vnd.dlna.mpeg-tts",
                "txt" => "text/plain",
                "u32" => "application/octet-stream",
                "uls" => "text/iuls",
                "user" => "text/plain",
                "ustar" => "application/x-ustar",
                "vb" => "text/plain",
                "vbdproj" => "text/plain",
                "vbk" => "video/mpeg",
                "vbproj" => "text/plain",
                "vbs" => "text/vbscript",
                "vcf" => "text/x-vcard",
                "vcproj" => "application/xml",
                "vcs" => "text/plain",
                "vcxproj" => "application/xml",
                "vddproj" => "text/plain",
                "vdp" => "text/plain",
                "vdproj" => "text/plain",
                "vdx" => "application/vnd.ms-visio.viewer",
                "vml" => "text/xml",
                "vscontent" => "application/xml",
                "vsct" => "text/xml",
                "vsd" => "application/vnd.visio",
                "vsi" => "application/ms-vsi",
                "vsix" => "application/vsix",
                "vsixlangpack" => "text/xml",
                "vsixmanifest" => "text/xml",
                "vsmdi" => "application/xml",
                "vspscc" => "text/plain",
                "vss" => "application/vnd.visio",
                "vsscc" => "text/plain",
                "vssettings" => "text/xml",
                "vssscc" => "text/plain",
                "vst" => "application/vnd.visio",
                "vstemplate" => "text/xml",
                "vsto" => "application/x-ms-vsto",
                "vsw" => "application/vnd.visio",
                "vsx" => "application/vnd.visio",
                "vtx" => "application/vnd.visio",
                "wav" => "audio/wav",
                "wave" => "audio/wav",
                "wax" => "audio/x-ms-wax",
                "wbk" => "application/msword",
                "wbmp" => "image/vnd.wap.wbmp",
                "wcm" => "application/vnd.ms-works",
                "wdb" => "application/vnd.ms-works",
                "wdp" => "image/vnd.ms-photo",
                "webarchive" => "application/x-safari-webarchive",
                "webtest" => "application/xml",
                "wiq" => "application/xml",
                "wiz" => "application/msword",
                "wks" => "application/vnd.ms-works",
                "wlmp" => "application/wlmoviemaker",
                "wlpginstall" => "application/x-wlpg-detect",
                "wlpginstall3" => "application/x-wlpg3-detect",
                "wm" => "video/x-ms-wm",
                "wma" => "audio/x-ms-wma",
                "wmd" => "application/x-ms-wmd",
                "wmf" => "application/x-msmetafile",
                "wml" => "text/vnd.wap.wml",
                "wmlc" => "application/vnd.wap.wmlc",
                "wmls" => "text/vnd.wap.wmlscript",
                "wmlsc" => "application/vnd.wap.wmlscriptc",
                "wmp" => "video/x-ms-wmp",
                "wmv" => "video/x-ms-wmv",
                "wmx" => "video/x-ms-wmx",
                "wmz" => "application/x-ms-wmz",
                "wpl" => "application/vnd.ms-wpl",
                "wps" => "application/vnd.ms-works",
                "wri" => "application/x-mswrite",
                "wrl" => "x-world/x-vrml",
                "wrz" => "x-world/x-vrml",
                "wsc" => "text/scriptlet",
                "wsdl" => "text/xml",
                "wvx" => "video/x-ms-wvx",
                "x" => "application/directx",
                "xaf" => "x-world/x-vrml",
                "xaml" => "application/xaml+xml",
                "xap" => "application/x-silverlight-app",
                "xbap" => "application/x-ms-xbap",
                "xbm" => "image/x-xbitmap",
                "xdr" => "text/plain",
                "xht" => "application/xhtml+xml",
                "xhtml" => "application/xhtml+xml",
                "xla" => "application/vnd.ms-excel",
                "xlam" => "application/vnd.ms-excel.addin.macroenabled.12",
                "xlc" => "application/vnd.ms-excel",
                "xld" => "application/vnd.ms-excel",
                "xlk" => "application/vnd.ms-excel",
                "xll" => "application/vnd.ms-excel",
                "xlm" => "application/vnd.ms-excel",
                "xls" => "application/vnd.ms-excel",
                "xlsb" => "application/vnd.ms-excel.sheet.binary.macroenabled.12",
                "xlsm" => "application/vnd.ms-excel.sheet.macroenabled.12",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xlt" => "application/vnd.ms-excel",
                "xltm" => "application/vnd.ms-excel.template.macroenabled.12",
                "xltx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
                "xlw" => "application/vnd.ms-excel",
                "xml" => "text/xml",
                "xmta" => "application/xml",
                "xof" => "x-world/x-vrml",
                "xoml" => "text/plain",
                "xpm" => "image/x-xpixmap",
                "xps" => "application/vnd.ms-xpsdocument",
                "xrm-ms" => "text/xml",
                "xsc" => "application/xml",
                "xsd" => "text/xml",
                "xsf" => "text/xml",
                "xsl" => "text/xml",
                "xslt" => "text/xml",
                "xsn" => "application/octet-stream",
                "xss" => "application/xml",
                "xtp" => "application/octet-stream",
                "xwd" => "image/x-xwindowdump",
                "z" => "application/x-compress",
                "zip" => "application/x-zip-compressed",
                _ => "application/octet-stream",
            };
        }
    }

    public class Message
    {
        public List<string> Images { get; set; } = new List<string>();
        public List<string> Audios { get; set; } = new List<string>();
        public string _Message { get; set; } = string.Empty;
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}