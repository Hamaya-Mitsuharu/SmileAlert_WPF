using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SmileAlert
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        Mat matFrame;
        const string FACE_DETECT_URL = "https://api-us.faceplusplus.com/facepp/v3/detect";
        const string API_KEY = "gvtqsN4gF5kfSQwRm3bEbFYgSSsrLGwH";
        const string API_SECRET = "52HOOEt4fb1-JAI5kHjRDWNPSBkFOyK3";

        Task task;
        public MainWindow()
        {
            InitializeComponent();
            task = CaptureAndSend(); // 非同期処理を開始
        }

        async Task CaptureAndSend()
        {
            // １秒周期で非同期実行
            await Task.Run(async () =>
            {
                // カメラキャプチャとHTTPリクエストの下準備
                var capture = new VideoCapture();
                capture.Open(0);
                if (!capture.IsOpened())
                {
                    throw new Exception("capture initialization failed");
                }
                var client = new HttpClient();

                while (true)
                {
                    // カメラ画像をキャプチャし、Matデータとして取得
                    matFrame = new Mat();
                    capture.Read(matFrame);
                    if (matFrame.Empty())
                    {
                        Debug.Print("キャプチャ画像が空でした");
                        continue;
                    }

                    // バイナリ画像データへ変換
                    // Mat -> Bytes -> Base64
                    var bytesImg = matFrame.ToBytes();
                    var base64Img = Convert.ToBase64String(bytesImg);

                    // HttpClientのhttps通信ではformの「Content-Type = multipart/form-data」は送れない
                    // 代わりにURIエンコードしたformの「Content-Type = application/x-www-form-urlencoded」なら遅れる
                    
                    // form用データ
                    Dictionary<String, String> formDict = new Dictionary<string, string>();
                    formDict.Add("api_key", API_KEY);
                    formDict.Add("api_secret", API_SECRET);
                    formDict.Add("image_base64", base64Img);
                    formDict.Add("return_attributes", "smiling");

                    // URIエンコードする
                    var encodedItems = formDict.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
                    var encodedContent = new StringContent(String.Join("&", encodedItems), null, "application/x-www-form-urlencoded");

                    var res = await client.PostAsync(FACE_DETECT_URL, encodedContent);
                    var response = await res.Content.ReadAsStringAsync();
                    // Debug.Print(res.ToString());     // リクエスト情報
                    // Debug.Print(response);           // レスポンス

                    // 笑顔率を表す"value"の値を取得する
                    var index = response.IndexOf("value");
                    if (index == -1)
                    {
                        Debug.Print("顔が見つかりませんでした");
                        matFrame.Dispose();
                        continue;
                    }
                    index += "value".Length + 1; // 「"value":」の「:」の位置に移動する

                    string valueStr = "";
                    int loopCnt = 0;
                    while (loopCnt < 10000)
                    {
                        index++;
                        if (response[index] == ',') break;
                        valueStr += response[index];

                        loopCnt++;
                        if (loopCnt > 10000) Debug.Print("カンマが見つかりませんでした");
                    }
                    float smilingPercent = float.Parse(valueStr);
                    
                    // 実行画面に文字列を表示
                    await ResultLabel.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            ResultLabel.Content = "笑顔率：" + smilingPercent.ToString();
                        })
                    );

                    // 実行画面にカメラ画像を表示
                    await Monitor.Dispatcher.BeginInvoke(
                       new Action(() =>
                       {
                           Monitor.Source = MatToImageSource(matFrame);
                       })
                    );
                    matFrame.Dispose();

                    Thread.Sleep(1);
                }
                // -- whileループ終了 -- 
                capture.Dispose();
                client.Dispose();
            });
        }

        // 下の MatToImageSource() メソッドに使う
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(System.IntPtr hObject);

        /// <summary>
        /// フレーム画像をImage.Sourceに使えるImageSourceにキャストする
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public ImageSource MatToImageSource(Mat img)
        {
            // HBitmapに変換
            var hBitmap = (img.ToBitmap()).GetHbitmap();
            ImageSource source;
            // HBitmapからBitmapSourceを作成
            try
            {
                source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    System.IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
                );
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
