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
        Mat frame;

        Task task;
        public MainWindow()
        {
            InitializeComponent();
            task = Count();
        }

        async Task Count()
        {
            var captureCount = 0;
            await Task.Run(() =>
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
                        captureCount++;
                        ResultLabel.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                ResultLabel.Content = captureCount.ToString();
                            })
                        );

                        //
                        // キャプチャ
                        //
                        frame = new Mat();
                        capture.Read(frame);
                        if (frame.Empty())
                        {
                            // return null;
                        }
                        Monitor.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                Monitor.Source = MatToImageSource(frame);
                            })
                        );

                        Thread.Sleep(100);
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
            Monitor.Source = MatToImageSource(frame);
            frame.Dispose();
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
                frame = new Mat();
                capture.Read(frame);
                if (frame.Empty())
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
