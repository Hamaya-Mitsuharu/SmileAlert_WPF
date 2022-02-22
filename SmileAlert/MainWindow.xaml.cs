using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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


namespace SmileAlert
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        Mat matFrame;
        private const string ATTRIBUTES = "gender,age";

        Task task;
        public MainWindow()
        {
            InitializeComponent();
            task = Count();
        }

        async Task Count()
        {
            var captureCount = 0;
            await Task.Run(async () =>
           {
               //
               // カメラを起動
               //
               using (var capture = new VideoCapture())
               {
                   capture.Open(0);

                   if (!capture.IsOpened())
                   {
                       throw new Exception("capture initialization failed");
                   }

                   while (true)
                   {
                       //
                       // キャプチャ回数をカウント
                       //
                       /*
                       captureCount++;
                       await ResultLabel.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               ResultLabel.Content = captureCount.ToString();
                           })
                       );
                       */

                       //
                       // キャプチャ
                       //
                       matFrame = new Mat();
                       capture.Read(matFrame);
                       if (matFrame.Empty())
                       {
                           // return null;
                       }

                       //
                       // Mat -> Bytes -> Base64
                       //
                       var bytesImg = matFrame.ToBytes();
                       var base64Img = Convert.ToBase64String(bytesImg);

                       var client = new HttpClient();
                       // var content = new StringContent(base64Img);

                       // HTTPヘッダの設定
                       var url = "https://api-us.faceplusplus.com/facepp/v3/detect";
                       var API_KEY = "gvtqsN4gF5kfSQwRm3bEbFYgSSsrLGwH";
                       var API_SECRET = "52HOOEt4fb1-JAI5kHjRDWNPSBkFOyK3";
                       
                       // request.Headers.Add("Content-Type", "multipart/form-data; boundary=----WebKitFormBoundaryxxxxyz");
                       // request.Headers.Add("api_key", API_KEY);
                       // request.Headers.Add("api_secret", API_SECRET);
                       // request.Headers.Add("image_base64", base64Img);
                       // request.Headers.Add("return_attributes", ATTRIBUTES);

                       string res = "";

                       var contentDict = new Dictionary<string, string>();
                       contentDict.Add("api_key", API_KEY);
                       contentDict.Add("api_secret", API_SECRET);
                       contentDict.Add("image_base64", base64Img);

                       using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                       using (var jsonContent = JsonContent.Create(contentDict))
                       {
                           request.Content = jsonContent;
                           request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");
                           // request.Content.Headers.Add("api_key", API_KEY);
                           // request.Content.Headers.Add("api_secret", API_SECRET);

                           var result = await client.SendAsync(request);
                           var status = result.StatusCode.ToString();
                           var json = await result.Content.ReadAsStringAsync();
                           var isSuccess = result.IsSuccessStatusCode.ToString();
                           res = result.ToString() + "\n" + status + "\n" + json + "\n" + isSuccess;
                       }
                       
                       await ResultLabel.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               ResultLabel.Content = res;
                           })
                       );


                       //
                       // 画面に表示
                       //
                       await Monitor.Dispatcher.BeginInvoke(
                          new Action(() =>
                          {
                              Monitor.Source = MatToImageSource(matFrame);
                          })
                      );
                       matFrame.Dispose();

                       Thread.Sleep(1000);
                   }
               }
           });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show("ボタンがクリックされました。");
            ResultLabel.Content = "クリックされました";
            UpdateImage();
        }

        void UpdateImage()
        {
            Capture();
            Monitor.Source = MatToImageSource(matFrame);
            matFrame.Dispose();
        }

        /// <summary>
        /// カメラ画像をキャプチャし、フレームに入れる
        /// </summary>
        public void Capture()
        {
            //
            // カメラを起動
            //
            using (var capture = new VideoCapture())
            {
                capture.Open(0);

                if (!capture.IsOpened())
                {
                    throw new Exception("capture initialization failed");
                }

                //
                // フレーム画像を取得
                //
                matFrame = new Mat();
                capture.Read(matFrame);
                if (matFrame.Empty())
                {
                    // return null;
                }
            }
        }

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
