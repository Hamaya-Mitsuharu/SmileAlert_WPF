using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                        Debug.Print("capture was empty");
                        continue;
                    }

                    // バイナリ画像データへ変換
                    // Mat -> Bytes -> Base64
                    var bytesImg = matFrame.ToBytes();
                    var base64Img = Convert.ToBase64String(bytesImg);

                    // POST通信のbodyに含めるFormDataを作成
                    var formDataBody = new MultipartFormDataContent("----FLICKR_MIME_20140415120129--");
                    formDataBody.Headers.ContentType.MediaType = "multipart/form-data";
                    // formDataBody.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    StringContent keyContent = new StringContent(API_KEY);
                    StringContent secretContent = new StringContent(API_SECRET);
                    StringContent imgContent = new StringContent(base64Img);
                    // keyContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                    // secretContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                    formDataBody.Add(keyContent, "api_key");
                    formDataBody.Add(secretContent, "api_secret");
                    formDataBody.Add(imgContent, "image_base64");

                    // リクエストを作成
                    // var uri = new Uri(FACE_DETECT_URL);
                    var request = new HttpRequestMessage(HttpMethod.Post, FACE_DETECT_URL);
 
                    // デバッグ用
                    // var request = new HttpRequestMessage(HttpMethod.Post, "http://httpbin.org/post");
                    
                    request.Content = imgContent;
                    Debug.Print(request.Content.Headers.ContentType.ToString());

                    // URIエンコードして送る
                    Dictionary<String, String> formDict = new Dictionary<string, string>();
                    formDict.Add("api_key", API_KEY);
                    formDict.Add("api_secret", API_SECRET);
                    formDict.Add("image_base64", base64Img);

                    var encodedItems = formDict.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
                    var encodedContent = new StringContent(String.Join("&", encodedItems), null, "application/x-www-form-urlencoded");


                    String response = "";
                    string responseContent = "";
                    try
                    {
                        var res = await client.PostAsync(FACE_DETECT_URL, encodedContent);
                        // var response = await client.PostAsync("http://httpbin.org/post", formDataBody);

                        client.DefaultRequestHeaders.Add("api_key", API_KEY);
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("multipart/form-data"));

                        // var res = await client.SendAsync(request);
                        response = res.ToString();
                        responseContent = await res.Content.ReadAsStringAsync();
                    }
                    catch (Exception e)
                    {
                        Debug.Print(e.Message);
                    }
                    
                    // request.Dispose();
                    formDataBody.Dispose();
                    keyContent.Dispose();
                    secretContent.Dispose();
                    // imgContent.Dispose();

                    // 実行画面に文字列を表示
                    string showResult = response + "\n" + responseContent;

                    await ResultLabel.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            ResultLabel.Content = showResult;
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

                    Thread.Sleep(5000);
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



        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    // MessageBox.Show("ボタンがクリックされました。");
        //    ResultLabel.Content = "クリックされました";
        //    UpdateImage();
        //}

        //void UpdateImage()
        //{
        //    Capture();
        //    Monitor.Source = MatToImageSource(matFrame);
        //    matFrame.Dispose();
        //}

        ///// <summary>
        ///// カメラ画像をキャプチャし、フレームに入れる
        ///// </summary>
        //public void Capture()
        //{
        //    //
        //    // カメラを起動
        //    //
        //    using (var capture = new VideoCapture())
        //    {
        //        capture.Open(0);

        //        if (!capture.IsOpened())
        //        {
        //            throw new Exception("capture initialization failed");
        //        }

        //        //
        //        // フレーム画像を取得
        //        //
        //        matFrame = new Mat();
        //        capture.Read(matFrame);
        //        if (matFrame.Empty())
        //        {
        //            // return null;
        //        }
        //    }
        //}
    }
}
