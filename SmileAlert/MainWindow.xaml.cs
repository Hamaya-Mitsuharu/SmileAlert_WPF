﻿using System;
using System.IO; // StreamReader
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; //  
using System.Net;
using System.Net.Http;
using System.Windows.Media;
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
        const string API_KEY = "gvtqsN4gF5kfSQwRm3bEbFYgSSsrLGwH";
        const string API_SECRET = "52HOOEt4fb1-JAI5kHjRDWNPSBkFOyK3";
        string thresholdTextPath = System.Environment.CurrentDirectory + "/threshold.txt";
        int sliderCount = 0;

        Task task;
        public MainWindow()
        {
            InitializeComponent();

            // 笑顔率のセーブデータを読み込み
            ContentRendered += (s, e) =>
            {
                using (StreamReader sr = new StreamReader(thresholdTextPath))
                {
                    float threshold = float.Parse(sr.ReadToEnd());
                    ThresholdSlider.Value = threshold;

                }
            };
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

                    // face++で使えるbase64（バイナリ）に変換
                    var bytesImg = matFrame.ToBytes();
                    var base64Img = Convert.ToBase64String(bytesImg);

                    StringContent encodedContent = UriEncodeWithDict(base64Img);

                    string response = await PostIntoFacePP(client, encodedContent);
                    encodedContent.Dispose();

                    float smilingPercent = GetSmilingPercent(response);
                    if (smilingPercent < 0.0f) continue; // 顔が見つからない場合

                    CheckSmilingPercent(smilingPercent);

                    ShowResultAndCapture(smilingPercent, matFrame);

                    Thread.Sleep(3000);
                }
                // -- whileループ終了 -- 
                capture.Dispose();
                client.Dispose();
            });
        }

        /*
        * HttpClientのhttps通信ではformの「Content-Type = multipart/form-data」は送れない
        * URIエンコードしたformの「Content-Type = application/x-www-form-urlencoded」なら送れる
        */
        StringContent UriEncodeWithDict(string base64Img)
        {
            Dictionary<String, String> formDict = new Dictionary<string, string>();
            formDict.Add("api_key", API_KEY);
            formDict.Add("api_secret", API_SECRET);
            formDict.Add("image_base64", base64Img);
            formDict.Add("return_attributes", "smiling");

            var encodedItems = formDict.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
            return new StringContent(String.Join("&", encodedItems), null, "application/x-www-form-urlencoded");
        }

        async Task<string> PostIntoFacePP(HttpClient client, HttpContent content)
        {
            const string FACE_DETECT_URL = "https://api-us.faceplusplus.com/facepp/v3/detect";

            var responseMessage = await client.PostAsync(FACE_DETECT_URL, content);
            var response = await responseMessage.Content.ReadAsStringAsync();

            // Debug.Print(res.ToString());     // レスポンス情報
            // Debug.Print(response);           // レスポンス内容

            return response;
        }

        float GetSmilingPercent(string response)
        {
            // 笑顔率を表す"value"の値を取得する
            var index = response.IndexOf("value");
            if (index == -1)
            {
                Debug.Print("顔が見つかりませんでした");
                matFrame.Dispose();
                return -1f;
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
                if (loopCnt > 10000)
                {
                    Debug.Print("異常：カンマが見つかりませんでした");
                    return -1f;
                }
            }
            return float.Parse(valueStr);
        }

        async void CheckSmilingPercent(float smilingPercent)
        {
            // 音声ファイルをロードします。
            // MediaAudio.LoadedBehavior = MediaState.Stop;
            // MediaAudio.Source = new Uri("E:\MyFiles\Projects\cSharp\SmileAlert\SmileAlert\Source\alert.wav");

            await Monitor.Dispatcher.BeginInvoke(
               new Action(() =>
               {
                   // 笑顔率が閾値より小さい場合警告
                   if (smilingPercent >= ThresholdSlider.Value) return;

                   Debug.Print("音を鳴らします");
                   MediaAudio.LoadedBehavior = MediaState.Stop;
                   MediaAudio.Source = new Uri("E:/MyFiles/Projects/cSharp/SmileAlert/SmileAlert/Source/alert.wav");
                   MediaAudio.LoadedBehavior = MediaState.Manual;
                   MediaAudio.Volume = 100;
                   MediaAudio.Play();
               })
            );
        }

        async void ShowResultAndCapture(float smilingPercent, Mat matFrame)
        {
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
        }

        // 下記の MatToImageSource() メソッドに使う
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern bool DeleteObject(System.IntPtr hObject);

        ImageSource MatToImageSource(Mat img)
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

        private void Slider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            var threshold = e.NewValue.ToString("F0");
            SliderValue.Content = threshold;

            // Slider_ValueChanged()は起動時に２回ほど呼ばれてしまう
            // 起動時に書き込んでしまうのを防ぐ
            if (sliderCount > 10)
            {
                File.WriteAllText(thresholdTextPath, threshold);
            }
            sliderCount++;
        }
    }
}
