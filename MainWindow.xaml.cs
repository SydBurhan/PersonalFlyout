using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace PersonalFlyout
{
    public sealed partial class MainWindow : Window
    {
        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private DispatcherTimer _hideTimer;
        private Microsoft.UI.Windowing.AppWindow _appWindow;

        private InMemoryRandomAccessStream? _currentColorStream;
        private InMemoryRandomAccessStream? _currentUIStream;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private DispatcherTimer _progressTimer;
        private DispatcherTimer _visualizerTimer;
        private Random _rnd = new Random();
        private MediaOverlaySuppressor _overlaySuppressor;
        private Microsoft.UI.Xaml.Media.Animation.Storyboard _showAnim;
        private Microsoft.UI.Xaml.Media.Animation.Storyboard _showAnimNext;
        private Microsoft.UI.Xaml.Media.Animation.Storyboard _showAnimPrev;
        private Microsoft.UI.Xaml.Media.Animation.Storyboard _hideAnim;
        private string _pendingAnimType = "Default";
        private string _lastTitle = string.Empty;
        private DateTime _lastTitleChangeTime = DateTime.MinValue;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // FIX: Remove White Border using DWM
            
            // 1. Set Rounded Corners for the System Window (Matches XAML CornerRadius="12")
            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
            int preference = 2;
            DwmSetWindowAttribute(hWnd, 33, ref preference, sizeof(int));

            // 2. Set System Border Color to None/Transparent
            // DWMWA_BORDER_COLOR = 34. Use 0xFFFFFFFF (None) or 0x001A1A1A (Dark)
            uint noneColor = 0xFFFFFFFF; 
            int noneColInt = unchecked((int)noneColor);
            DwmSetWindowAttribute(hWnd, 34, ref noneColInt, sizeof(int));



            // FIX 2: Extend Frame into Client Area (Glass Effect) to fix White Corners
            MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hWnd, ref margins);

            if (_appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
                // presenter.SetBorderAndTitleBar(false, false); // Removing this as it conflicts with ExtendsContentIntoTitleBar
            }

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;
            }

            _appWindow.Resize(new Windows.Graphics.SizeInt32(400, 120));
            _appWindow.Move(new Windows.Graphics.PointInt32(-2000, -2000));

            // Start suppressing Windows media overlay
            _overlaySuppressor = new MediaOverlaySuppressor();
            _overlaySuppressor.MediaCommandReceived += (cmd) => 
            {
                _pendingAnimType = cmd;
                
                // Safety timeout (same as button click)
                var currentType = _pendingAnimType;
                _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => 
                { 
                    if (_pendingAnimType == currentType) _pendingAnimType = "Default"; 
                }, System.Threading.Tasks.TaskScheduler.Default);
            };
            _overlaySuppressor.Start();

            SetupMediaListener();
            this.Closed += (s, e) => 
            { 
                _overlaySuppressor?.Stop();
                Environment.Exit(0); 
            };

            // Load Storyboards
            ((FrameworkElement)this.Content).Loaded += (s, e) =>
            {
               _showAnim = (Microsoft.UI.Xaml.Media.Animation.Storyboard)RootGrid.Resources["ShowAnimation"];
               _showAnimNext = (Microsoft.UI.Xaml.Media.Animation.Storyboard)RootGrid.Resources["ShowAnimationNext"];
               _showAnimPrev = (Microsoft.UI.Xaml.Media.Animation.Storyboard)RootGrid.Resources["ShowAnimationPrev"];
               _hideAnim = (Microsoft.UI.Xaml.Media.Animation.Storyboard)RootGrid.Resources["HideAnimation"];
               _hideAnim.Completed += (s2, e2) => _appWindow.Move(new Windows.Graphics.PointInt32(-2000, -2000));

               // STARTUP GREETING:
               // Briefly show the flyout so the user knows it started successfully.
               this.DispatcherQueue.TryEnqueue(async () => 
               {
                   await System.Threading.Tasks.Task.Delay(1000); // Wait for system to settle
                   
                   // Set dummy text if empty
                   if (string.IsNullOrEmpty(TxtTitle.Text))
                   {
                        TxtTitle.Text = "Personal Flyout";
                        TxtArtist.Text = "Ready to play";
                   }

                   // Show
                   _appWindow.Move(new Windows.Graphics.PointInt32(40, 10));
                   this.Activate();
                   _showAnim?.Begin();

                   // Hide after 3 seconds
                   await System.Threading.Tasks.Task.Delay(3000);
                   _hideTimer.Start(); // This triggers the hide animation
               });
            };
        }

        private async void SetupMediaListener()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _hideTimer.Tick += (s, e) => 
            { 
                _hideAnim?.Begin(); // Play hide animation
                _hideTimer.Stop(); 
            };
            
            // Visualizer Timer (Fast updates for animation)
            _visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _visualizerTimer.Tick += (s, e) => UpdateVisualizer();

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (s, e) => UpdateProgress();

            Action attachEvents = () =>
            {
                var session = _manager.GetCurrentSession();
                if (session != null)
                {
                    _currentSession = session;
                    session.MediaPropertiesChanged += (s, e) => UpdateUI();
                    session.PlaybackInfoChanged += (s, e) => UpdatePlaybackState();
                }
            };

            _manager.CurrentSessionChanged += (s, e) => { attachEvents(); UpdateUI(); };
            attachEvents();
            UpdateUI();
        }

        private void UpdateUI()
        {
            Task.Run(async () =>
            {
                try
                {
                    var session = _manager?.GetCurrentSession();
                    if (session == null) return;

                    var props = await session.TryGetMediaPropertiesAsync();
                    if (props == null) return;
                    // _lastTrackId check removed to allow window to show on Pause/Play
                    // _lastTrackId = props.Title;

                    Windows.UI.Color? dominantColor = null;
                    InMemoryRandomAccessStream? uiStream = null;

                    if (props.Thumbnail != null)
                    {
                        try
                        {
                            using var originalStream = await props.Thumbnail.OpenReadAsync();

                            _currentColorStream?.Dispose();
                            _currentColorStream = new InMemoryRandomAccessStream();
                            await RandomAccessStream.CopyAsync(originalStream, _currentColorStream);
                            _currentColorStream.Seek(0);

                            dominantColor = await ExtractColorSync(_currentColorStream);

                            originalStream.Seek(0);
                            _currentUIStream?.Dispose();
                            _currentUIStream = new InMemoryRandomAccessStream();
                            await RandomAccessStream.CopyAsync(originalStream, _currentUIStream);
                            _currentUIStream.Seek(0);

                            uiStream = _currentUIStream;
                        }
                        catch { }
                    }

                    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () =>
                    {
                        _appWindow.Move(new Windows.Graphics.PointInt32(40, 10));
                        this.Activate();
                        
                        _appWindow.Move(new Windows.Graphics.PointInt32(40, 10));
                        this.Activate();
                        
                        // Only use Directional Animation if the Title CHANGED (New Song)
                        if (props.Title != _lastTitle)
                        {
                            if (_pendingAnimType == "Next") { _showAnimNext?.Begin(); }
                            else if (_pendingAnimType == "Prev") { _showAnimPrev?.Begin(); }
                            else { _showAnim?.Begin(); }
                            
                            _pendingAnimType = "Default"; // Consume the flag
                            _lastTitle = props.Title;
                            _lastTitleChangeTime = DateTime.Now;
                        }
                        else
                        {
                            // Same song (e.g. Pause/Play) -> Default animation
                            // Only play default if it's been more than 500ms since the last title change
                            // This prevents the "Default" animation from overriding the "Directional" animation
                            if ((DateTime.Now - _lastTitleChangeTime).TotalMilliseconds > 500)
                            {
                                _showAnim?.Begin();
                            }
                        }

                        _hideTimer.Stop();
                        _hideTimer.Start();

                        if (dominantColor.HasValue)
                        {
                            BackgroundLayer.Background = CreateGradientFromColor(dominantColor.Value);
                        }
                        else
                        {
                            BackgroundLayer.Background = CreateGradientFromColor(Windows.UI.Color.FromArgb(255, 30, 30, 30));
                        }

                        TxtTitle.Text = props.Title;
                        TxtArtist.Text = props.Artist;

                        if (uiStream != null)
                        {
                            try
                            {
                                var bitmap = new BitmapImage { DecodePixelWidth = 80 };
                                await bitmap.SetSourceAsync(uiStream);
                                AlbumArtBorder.Background = new ImageBrush 
                                { 
                                    ImageSource = bitmap, 
                                    Stretch = Stretch.UniformToFill,
                                    RelativeTransform = new ScaleTransform { CenterX = 0.5, CenterY = 0, ScaleX = 1.25, ScaleY = 1.25 }
                                };
                            }
                            catch { }
                        }
                        
                        UpdatePlaybackState();
                        UpdateProgress();
                    });
                }
                catch { }
            });
        }

        private void UpdatePlaybackState()
        {
            if (_currentSession == null) return;
            try
            {
                var info = _currentSession.GetPlaybackInfo();
                this.DispatcherQueue.TryEnqueue(() => 
                {
                    if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        _progressTimer.Start();
                        _visualizerTimer.Start();
                        IconPlayPause.Symbol = Symbol.Pause;
                    }
                    else 
                    {
                        _progressTimer.Stop();
                        _visualizerTimer.Stop();
                        IconPlayPause.Symbol = Symbol.Play;
                    }
                });
            } catch {}
        }

        private void UpdateProgress()
        {
            if (_currentSession == null) return;
            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                if (timeline != null)
                {
                    var current = timeline.Position.TotalSeconds;
                    var total = timeline.EndTime.TotalSeconds;
                    if (total > 0)
                    {
                         this.DispatcherQueue.TryEnqueue(() => 
                         {
                             ProgressBar.Maximum = total;
                             ProgressBar.Value = current;
                             TxtCurrentTime.Text = FormatTime(current);
                             TxtTotalTime.Text = FormatTime(total);
                         });
                    }
                }
            } 
            catch {}
        }

        private string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
        }

        private LinearGradientBrush CreateGradientFromColor(Windows.UI.Color color)
        {
            var darker = Windows.UI.Color.FromArgb(255, (byte)(color.R * 0.5), (byte)(color.G * 0.5), (byte)(color.B * 0.5));
            var lighter = Windows.UI.Color.FromArgb(255, (byte)Math.Min(255, color.R * 1.8), (byte)Math.Min(255, color.G * 1.8), (byte)Math.Min(255, color.B * 1.8));

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = darker, Offset = 0 },
                    new GradientStop { Color = color, Offset = 0.5 },
                    new GradientStop { Color = lighter, Offset = 1 }
                }
            };
        }

        private async Task<Windows.UI.Color> ExtractColorSync(IRandomAccessStream stream)
        {
            try
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var transform = new BitmapTransform { ScaledWidth = 2, ScaledHeight = 2 };

                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                byte[] pixelData = new byte[16];
                softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

                int totalR = 0, totalG = 0, totalB = 0;

                for (int i = 0; i < 4; i++)
                {
                    int offset = i * 4;
                    totalB += pixelData[offset];
                    totalG += pixelData[offset + 1];
                    totalR += pixelData[offset + 2];
                }

                byte r = (byte)(totalR / 4);
                byte g = (byte)(totalG / 4);
                byte b = (byte)(totalB / 4);

                if (r == 0 && g == 0 && b == 0)
                {
                    r = 45; g = 50; b = 60;
                }
                else
                {
                    r = (byte)Math.Min(255, r * 1.8);
                    g = (byte)Math.Min(255, g * 1.8);
                    b = (byte)Math.Min(255, b * 1.8);
                    if (r < 40 && g < 40 && b < 40) { r += 40; g += 40; b += 40; }
                }

                return Windows.UI.Color.FromArgb(255, r, g, b);
            }
            catch
            {
                return Windows.UI.Color.FromArgb(255, 30, 30, 30);
            }
        }

        private void UpdateVisualizer()
        {
            // Simple random height simulation for visualizer bars
            VisBar1.Height = _rnd.Next(4, 18);
            VisBar2.Height = _rnd.Next(4, 18);
            VisBar3.Height = _rnd.Next(4, 18);
            VisBar4.Height = _rnd.Next(4, 18);
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            _pendingAnimType = "Prev";
            // Safety timeout: Reset flag if no song change happens within 2s
            var currentType = _pendingAnimType;
            _ = Task.Delay(2000).ContinueWith(_ => 
            { 
                if (_pendingAnimType == currentType) _pendingAnimType = "Default"; 
            }, TaskScheduler.Default);

            if (_currentSession != null) await _currentSession.TrySkipPreviousAsync();
        }

        private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession != null) await _currentSession.TryTogglePlayPauseAsync();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _pendingAnimType = "Next";
             // Safety timeout
            var currentType = _pendingAnimType;
            _ = Task.Delay(2000).ContinueWith(_ => 
            { 
                if (_pendingAnimType == currentType) _pendingAnimType = "Default"; 
            }, TaskScheduler.Default);

            if (_currentSession != null) await _currentSession.TrySkipNextAsync();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }
    }
}
