using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.Kinect;

namespace KinectV2
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // Kinect SDK
        KinectSensor kinect;

        ColorFrameReader colorFrameReader;
        FrameDescription colorFrameDesc;
        ColorImageFormat colorFormat = ColorImageFormat.Bgra;

        BodyFrameReader bodyFrameReader;
        Body[] bodies;

        // WPF
        WriteableBitmap colorBitmap;
        byte[] colorBuffer;
        int colorStride;
        Int32Rect colorRect;

        float scaleX = 1;
        float scaleY = 1;


        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// ウィンドウがロードされるときに呼ばれる(初期化処理)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            try {
                // Kinectを開く
                {
                    kinect = KinectSensor.GetDefault();
                    kinect.Open();
                }

                // カラーカメラの初期化
                {
                    // カラーリーダーを開く
                    colorFrameReader = kinect.ColorFrameSource.OpenReader();
                    colorFrameReader.FrameArrived += colorFrameReader_FrameArrived;

                    // カラー画像の情報を作成する(BGRAフォーマット)
                    colorFrameDesc = kinect.ColorFrameSource.CreateFrameDescription( colorFormat );

                    // カラー用のビットマップを作成する
                    colorBitmap = new WriteableBitmap(
                                        colorFrameDesc.Width, colorFrameDesc.Height,
                                        96, 96, PixelFormats.Bgra32, null );
                    colorStride = colorFrameDesc.Width * (int)colorFrameDesc.BytesPerPixel;
                    colorRect = new Int32Rect( 0, 0,
                                        colorFrameDesc.Width, colorFrameDesc.Height );
                    colorBuffer = new byte[colorStride * colorFrameDesc.Height];
                    ImageColor.Source = colorBitmap;
                }

                // Bodyの初期化
                {

                    // Bodyを入れる配列を作る
                    bodies = new Body[kinect.BodyFrameSource.BodyCount];

                    // ボディーリーダーを開く
                    bodyFrameReader = kinect.BodyFrameSource.OpenReader();
                    bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;
                }

                // 画像サイズによる拡大率を設定
                scaleX = colorFrameDesc.Width / (float)ImageColor.Width;
                scaleY = colorFrameDesc.Height / (float)ImageColor.Height;
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
                Close();
            }
        }

        /// <summary>
        /// ウィンドウが閉じられる時に呼ばれる(終了処理)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            if ( colorFrameReader != null ) {
                colorFrameReader.Dispose();
                colorFrameReader = null;
            }

            if ( bodyFrameReader != null ) {
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            if ( kinect != null ) {
                kinect.Close();
                kinect = null;
            }
        }

        /// <summary>
        /// カラーのフレーム更新イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void colorFrameReader_FrameArrived( object sender, ColorFrameArrivedEventArgs e )
        {
            // カラーフレームを取得する
            using ( var colorFrame = e.FrameReference.AcquireFrame() ) {
                if ( colorFrame == null ) {
                    return;
                }

                // BGRAデータを取得する
                colorFrame.CopyConvertedFrameDataToArray( colorBuffer, colorFormat );

                // ビットマップにする
                colorBitmap.WritePixels( colorRect, colorBuffer, colorStride, 0 );
            }
        }

        /// <summary>
        /// Bodyのフレーム更新イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived( object sender, BodyFrameArrivedEventArgs e )
        {
            // Bodyフレームを取得する
            using ( var bodyFrame = e.FrameReference.AcquireFrame() ) {
                if ( bodyFrame == null ) {
                    return;
                }

                // ボディデータを取得する
                bodyFrame.GetAndRefreshBodyData( bodies );

                // ボディの表示
                CanvasBody.Children.Clear();

                foreach ( var body in bodies.Where( b => b.IsTracked ) ) {
                    foreach ( var joint in body.Joints ) {
                        // 手の位置が追跡状態
                        if ( joint.Value.TrackingState == TrackingState.Tracked ) {
                            DrawEllipse( joint.Value, Brushes.Blue );
                        }
                        // 手の位置が推測状態
                        else if ( joint.Value.TrackingState == TrackingState.Inferred ) {
                            DrawEllipse( joint.Value, Brushes.Yellow );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 関節位置を表示する
        /// </summary>
        /// <param name="joint"></param>
        /// <param name="R"></param>
        /// <param name="color"></param>
        private void DrawEllipse( Joint joint, Brush color )
        {
#if true
            // 円の半径
            const int R = 5;

            var ellipse = new Ellipse()
            {
                Width = R * 2,
                Height =  R * 2,
                Fill = color,
            };
#else
            // 円の半径
            int R = (int)(joint.Position.Z * 10);

            var ellipse = new Ellipse()
            {
                Width = R * 2,
                Height =  R * 2,
                Fill = color,
            };
#endif

            // カメラ座標系をカラー座標系に変換する
            var point = kinect.CoordinateMapper.MapCameraPointToColorSpace( joint.Position );
            if ( (point.X < 0) || (point.Y < 0) ) {
                return;
            }

            // カラー座標系で円を配置する
            Canvas.SetLeft( ellipse, (point.X / scaleX) - R );
            Canvas.SetTop( ellipse, (point.Y / scaleY) - R );

            CanvasBody.Children.Add( ellipse );
        }
    }
}
