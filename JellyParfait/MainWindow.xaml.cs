﻿using DiscordRPC;
using JellyParfait.Model;
using JellyParfait.Windows;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YoutubeExplode;
using YoutubeExplode.Converter;

namespace JellyParfait {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow {

        /// <summary>
        /// フォルダへのパス
        /// </summary>
        public static readonly string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\";

        public static string cachePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\cache\";

        public static string mp3Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\mp3\";

        private readonly FavoriteTextFile FileReader = new FavoriteTextFile();

        private DiscordRpcClient _discordClient = new DiscordRpcClient("896321734796533760");

        /// <summary>
        /// 音楽の情報
        /// </summary>
        private AudioFileReader media;

        /// <summary>
        /// プレイヤー
        /// </summary>
        private WaveOutEvent player;

        /// <summary>
        /// スライダーにクリックのときのフラグ
        /// </summary>
        private bool sliderClick;

        /// <summary>
        /// キュー
        /// </summary>
        private List<MusicData> queue = new List<MusicData>();

        /// <summary>
        /// 現在再生されているキュー
        /// </summary>
        private int nowQueue = -1;

        /// <summary>
        /// 連打してはいけないボタンのフラグ
        /// </summary>
        private bool Clicked;

        /// <summary>
        /// 正常に再生できているか確認するフラグ
        /// </summary>
        private bool Complete;

        /// <summary>
        /// 検索中か確認するフラグ
        /// </summary>
        private string Searched = String.Empty;

        /// <summary>
        /// ダウンロード中か確認するフラグ
        /// </summary>
        private bool download;

        /// <summary>
        /// 初起動か確認するフラグ
        /// </summary>
        private bool first;

        /// <summary>
        /// 押されたマウスのボタンを格納
        /// </summary>
        private MouseButton mouseButton;

        private bool doPlay;

        private EqualizerBand[] bands;

        public ConfigFile _settings;


        public MainWindow() {
            InitializeComponent();
            if (!Directory.Exists(cachePath)) first = true;
            Directory.CreateDirectory(cachePath);
            Directory.CreateDirectory(path + "favorite");
            Directory.CreateDirectory(mp3Path);
            _settings = new ConfigFile();
            _discordClient.Initialize();
            ResetRichPrecense();
            bands = new EqualizerBand[]{
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 32, Gain = _settings.config.EQ_32},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 64, Gain = _settings.config.EQ_64},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 125, Gain = _settings.config.EQ_125},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 250, Gain = _settings.config.EQ_250},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 500, Gain = _settings.config.EQ_500},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 1000, Gain = _settings.config.EQ_1000},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 2000, Gain = _settings.config.EQ_2000},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 4000, Gain = _settings.config.EQ_4000},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 8000, Gain = _settings.config.EQ_8000},
                    new EqualizerBand {Bandwidth = 0.8f, Frequency = 12000, Gain = _settings.config.EQ_12000},
                };

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = true;
            Hide();
        }

        private void Window_Closed(object sender, EventArgs e) {
            if (IsPlay()) {
                Stop();
                player.Dispose();
            }
            _settings.config.Volume = (float)VolumeSlider.Value;
            _settings.config.EQ_32 = GetEqualizer()[0].Gain;
            _settings.config.EQ_64 = GetEqualizer()[1].Gain;
            _settings.config.EQ_125 = GetEqualizer()[2].Gain;
            _settings.config.EQ_250 = GetEqualizer()[3].Gain;
            _settings.config.EQ_500 = GetEqualizer()[4].Gain;
            _settings.config.EQ_1000 = GetEqualizer()[5].Gain;
            _settings.config.EQ_2000 = GetEqualizer()[6].Gain;
            _settings.config.EQ_4000 = GetEqualizer()[7].Gain;
            _settings.config.EQ_8000 = GetEqualizer()[8].Gain;
            _settings.config.EQ_12000 = GetEqualizer()[9].Gain;
            _settings.config.DiscordActivity = Discord_Share.IsChecked;
            _settings.Write();
            App.DeleteNotifyIcon();

        }

        private async void Cache_Click(object sender, RoutedEventArgs e) {
            if (download) {
                await this.ShowMessageAsync("JellyParfait", "現在キャッシュダウンロード中です。ダウンロードが終わるまでお待ち下さい。");
                return;
            }
            var Directory = new DirectoryInfo(cachePath);
            double FilesSize = GetDirectorySize(Directory);
            var msgbox = await this.ShowMessageAsync("JellyParfait", "現在のキャッシュは約" + FilesSize.ToString() + "MBです\n削除しますか？\n(再生中は音楽が停止し、キューがリセットされます)", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings() {
                AffirmativeButtonText = "はい",
                NegativeButtonText = "いいえ"
            });
            if (msgbox == MessageDialogResult.Negative) return;
            Reset();
            foreach (FileInfo file in Directory.GetFiles()) {
                try {
                    file.Delete();
                } catch { }
            }
        }

        public async void Exit_Click(object sender, RoutedEventArgs e) {
            if (download) {
                var msgbox = await this.ShowMessageAsync("JellyParfait", "現在キャッシュダウンロード中です。\n今終了するとキャッシュファイルが破損する可能性がありますが終了しますか？", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings() {
                    AffirmativeButtonText = "はい",
                    NegativeButtonText = "いいえ"
                });
                if (msgbox == MessageDialogResult.Negative) return;
            }
            if (IsPlay()) {
                Stop();
                player.Dispose();
            }
            Application.Current.Shutdown();
        }

        public async void Version_Infomation_Click(object sender, RoutedEventArgs e) {
            await this.ShowMessageAsync("JellyParfait", "JellyParfait version 0.9.9.2β\n\nCopylight(C)2021 yurisi\nAll rights reserved.\n\n本ソフトウェアはオープンソースソフトウェアです。\nGPL-3.0 Licenseに基づき誰でも複製や改変ができます。\n\nGithub\nhttps://github.com/yurisi0212/JellyParfait"); ;
        }

        private void Twitter_Click(object sender, RoutedEventArgs e) {
            if (player != null) {
                if (player.PlaybackState != PlaybackState.Stopped) {
                    string str;
                    if (queue[nowQueue].Id == "local") {
                        str = WebUtility.UrlEncode("Now Playing...\n「" + queue[nowQueue].Title + "」\n" + "#JellyParfait #NowPlaying");
                        Process.Start(new ProcessStartInfo("cmd", $"/c start https://twitter.com/intent/tweet?text=" + str) { CreateNoWindow = true });
                        return;
                    }
                    str = WebUtility.UrlEncode("Now Playing...\n「" + queue[nowQueue].Title + "」\n" + queue[nowQueue].YoutubeUrl + "\n#JellyParfait #NowPlaying");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start https://twitter.com/intent/tweet?text=" + str) { CreateNoWindow = true });
                    return;
                }
            }
            MessageBox.Show("現在何も再生されていません。", "JellyParfait", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void Discord_Share_Click(object sender, RoutedEventArgs e) {
            Discord_Share.IsChecked = !_settings.config.DiscordActivity;
            _settings.config.DiscordActivity = Discord_Share.IsChecked;
            if (!_settings.config.DiscordActivity) {
                _discordClient.Dispose();
                _discordClient = new DiscordRpcClient("896321734796533760");
            } else {
                MessageBox.Show("再起動するか、時間が立たないと表示されない場合があります。", "JellyParfait", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetRichPrecense();
            }
        }

        private void Discord_Share_Loaded(object sender, RoutedEventArgs e) {
            Discord_Share.IsChecked = _settings.config.DiscordActivity;
        }

        private void Equalizer_Click(object sender, RoutedEventArgs e) {
            EqualizerWindow window = new EqualizerWindow(this);
            window.Show();
        }

        public async void ReadPlayList_Click(object sender, RoutedEventArgs e) {
            var open = new OpenFileDialog() {
                Title = "お気に入りファイルの選択",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\favorite\",
                Filter = "お気に入りファイル(*.favo;*.favorite)|*.favo;*.favorite|テキストファイル(*.txt;*.text)|*.txt;*.text",
                Multiselect = false
            };
            if (open.ShowDialog() != true) return;
            if (queue.Count != 0) {
                var msgbox = await this.ShowMessageAsync("JellyParfait", "キューをリセットしますか？", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings() {
                    AffirmativeButtonText = "はい",
                    NegativeButtonText = "いいえ"
                });
                if (msgbox == MessageDialogResult.Affirmative) Reset();
            }
            var fileUrls = FileReader.GetURLs(open.FileName);
            var progress = await this.ShowProgressAsync("JellyParfait", "ダウンロード中...", true);
            foreach (var Url in fileUrls.Select((value, index) => new { value, index })) {
                if (queue.Exists(x => x.YoutubeUrl == Url.value)) {
                    var msgbox = MessageBox.Show(this, "既に存在しているようです。追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (msgbox == MessageBoxResult.No) return;
                }
                if (Url.value[0..4] == "http") {
                    await Task.Run(() => AddQueue(Url.value));
                } else {
                    if (File.Exists(Url.value)) {
                        var data = new MusicData(this) {
                            QueueId = queue.Count,
                            Title = Path.GetFileNameWithoutExtension(Url.value),
                            Id = "local",
                            Url = Url.value,
                            YoutubeUrl = Url.value,
                            Thumbnails = null,
                            Visibility = Visibility.Hidden,
                            Color = "white",
                        };
                        queue.Add(data);
                    } else {
                        MessageBox.Show(this, Url.value + "に音楽は存在しません。", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }
                }
                progress.SetProgress((float)Url.index / (float)fileUrls.Count);
            }
            ReloadListView();
            await progress.CloseAsync();
        }

        public void SavePlayList_Click(object sender, RoutedEventArgs e) {
            if (queue.Count != 0) {
                FileReader.Save(queue);
            } else {
                MessageBox.Show("現在キューに音楽が入っていません。", "JellyParfait", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Mp3_Click(object sender, RoutedEventArgs e) {
            var open = new OpenFileDialog() {
                Title = "mp3ファイルの選択",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "mp3ファイル" + "|*.mp3",
                Multiselect = true,
            };

            if (open.ShowDialog() != true) return;

            foreach (var filename in open.FileNames) {
                if (!File.Exists(mp3Path + Path.GetFileName(filename)))
                    File.Copy(filename, mp3Path + Path.GetFileName(filename));

                var data = new MusicData(this) {
                    QueueId = queue.Count,
                    Title = Path.GetFileNameWithoutExtension(filename),
                    Id = "local",
                    Url = mp3Path + Path.GetFileName(filename),
                    YoutubeUrl = mp3Path + Path.GetFileName(filename),
                    Thumbnails = null,
                    Visibility = Visibility.Hidden,
                    Color = "white",
                };
                queue.Add(data);
            }
            ReloadListView();
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) Search();
        }

        private async void SearchTextBox_Loaded(object sender, RoutedEventArgs e) {
            if (first) {
                var settings = new MetroDialogSettings {
                    //DefaultText = "https://www.youtube.com/watch?list=PL1kIh8ZwhZzKMU8MELWCfveQifBZWUIhi",
                };
                var dialog = await this.ShowInputAsync("ようこそJellyParfaitへ！", "youtubeのURLを入力してください！(プレイリストでもOK)\n初回はキャッシュのダウンロードがあるので時間がかかります", settings);
                if (dialog == null) return;
                if (dialog == String.Empty) return;
                searchTextBox.Text = dialog;
                Search();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            Search();
        }

        private void MusicQueue_MouseUp(object sender, MouseButtonEventArgs e) {
            mouseButton = e.ChangedButton;
        }

        private void MusicQueue_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (nowQueue != MusicQueue.SelectedIndex) {
                if (MusicQueue.SelectedIndex != -1) {
                    SetQueue(MusicQueue.SelectedIndex);
                }
            }
            MusicQueue.SelectedIndex = -1;
        }

        private void MusicQueue_Loaded(object sender, RoutedEventArgs e) {
            Binding myBinding = new Binding();
            myBinding.Source = queue;
            myBinding.Delay = 1000;
            BindingOperations.SetBinding(MusicQueue, ItemsControl.ItemsSourceProperty, myBinding);
        }

        private void MusicTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            //Debug.Print(sliderClick.ToString());
        }

        private void MusicTimeSlider_PreviewMouseUp(object sender, DragCompletedEventArgs e) {
            if (player == null) {
                MusicTimeSlider.Value = 0;
            } else {
                sliderClick = true;
                player.Pause();
                media.Position = (long)(media.WaveFormat.AverageBytesPerSecond * Math.Floor(MusicTimeSlider.Value));
                SetTime(media.CurrentTime);
                doPlay = false;
            }
        }

        private void MusicTimeSlider_PreviewMouseDown(object sender, DragStartedEventArgs e) {
            if (player == null) {
                MusicTimeSlider.Value = 0;
            } else {
                doPlay = true;
                player.Pause();
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e) {
            Prev();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e) {
            if (Clicked) return;
            if (player == null) return;
            (IsPlay() ? (Action)Pause : Play)();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) {
            Next();
        }

        private void VolumeSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            if (player != null) {
                player.Volume = (float)(VolumeSlider.Value * 0.025);
            }
        }
        private void VolumeSlider_Loaded(object sender, RoutedEventArgs e) {
            VolumeSlider.Value = _settings.config.Volume;
        }

        private void Search() {
            if (searchTextBox.Text == string.Empty) return;
            if (searchTextBox.Text == Searched) {
                var msgbox = MessageBox.Show(this, "現在検索しているようです。もう一度追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (msgbox == MessageBoxResult.No) return;
            }
            CheckURL(searchTextBox.Text);
        }

        private async void CheckURL(string youtubeUrl) {
            Searched = youtubeUrl;
            var progress = await this.ShowProgressAsync("JellyParfait", "ダウンロード中...(時間がかかる場合があります。)", true);
            try {
                var youtube = new YoutubeClient();
                var playlist = await youtube.Playlists.GetAsync(youtubeUrl);
                var videos = youtube.Playlists.GetVideosAsync(playlist.Id);
                var playlistcount = 0;
                await foreach (var video in videos) {
                    playlistcount += 1;
                }
                var count = 0;
                await foreach (var video in videos) {
                    count += 1;
                    progress.SetProgress((float)count / (float)playlistcount);
                    if (queue.Exists(x => x.YoutubeUrl == video.Url)) {
                        var msgbox = MessageBox.Show(this, video.Title + "\n既に存在しているようです。追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (msgbox == MessageBoxResult.No) continue;
                    }
                    await Task.Run(() => AddQueue(video.Url));
                }

            } catch (Exception e) {
                if (e is ArgumentException || e is YoutubeExplode.Exceptions.PlaylistUnavailableException) {
                    if (queue.Exists(x => x.YoutubeUrl == youtubeUrl)) {
                        var msgbox = MessageBox.Show(this, "既に存在しているようです。追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (msgbox == MessageBoxResult.No) return;
                    }
                    await Task.Run(() => AddQueue(youtubeUrl));
                    progress.SetProgress(1);
                }
            } finally {
                Searched = String.Empty;
                await progress.CloseAsync();
            }
        }

        private void AddQueue(string youtubeUrl) {
            try {
                MusicData musicData = null;
                musicData = GetVideoObject(youtubeUrl).Result;
                if (musicData == null) return;
                if (musicData.Url == string.Empty) return;

                queue.Add(musicData);
                Dispatcher.Invoke(() => ReloadListView());

            } catch (AggregateException e) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\n有効な動画ではありませんでした。(ライブ配信は対応していません。)\n" + e.Message, "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }

        private async Task<MusicData> GetVideoObject(string youtubeUrl) {
            try {
                var youtubeClient = new YoutubeClient();
                var video = await youtubeClient.Videos.GetAsync(youtubeUrl);
                var music = cachePath + video.Id + ".mp3";
                var image = cachePath + video.Id + ".jpg";

                if (!File.Exists(music)) {
                    if (TimeSpan.Compare(video.Duration.Value, new TimeSpan(0, 30, 0)) == 1) {
                        var result = false;
                        Dispatcher.Invoke(() => {
                            var msgbox = MessageBox.Show(this, "「" + video.Title + "」\n" + "この動画は30分を超えています。\nダウンロードに時間がかかり、空き容量を多く使う可能性がありますがよろしいでしょうか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (msgbox == MessageBoxResult.No) result = true;
                        });
                        if (result) return null;
                    }
                    await youtubeClient.Videos.DownloadAsync(video.Id, music);
                }

                if (!File.Exists(image)) {
                    using WebClient client = new WebClient();
                    await Task.Run(() => {
                        try {
                            client.DownloadFile(new Uri("https://img.youtube.com/vi/" + video.Id + "/maxresdefault.jpg"), image);
                        } catch (WebException) {
                            try {
                                client.DownloadFile(new Uri("https://img.youtube.com/vi/" + video.Id + "/sddefault.jpg"), image);
                            } catch (WebException) {
                                client.DownloadFile(new Uri("https://img.youtube.com/vi/" + video.Id + "/default.jpg"), image);
                            }
                        }
                    });
                }

                return new MusicData(this) {
                    QueueId = queue.Count,
                    Title = video.Title,
                    Id = video.Id,
                    Url = music,
                    YoutubeUrl = "https://www.youtube.com/watch?v=" + video.Id,
                    Thumbnails = image,
                    Visibility = Visibility.Hidden,
                    Color = "white",
                };
            } catch (System.Net.Http.HttpRequestException e) {
                await Dispatcher.Invoke(async () => {
                    var msgbox = MessageBox.Show(this, "Error\nキャッシュダウンロード中にエラーが発生しました\nインターネットに接続されているか確認してください\n再ダウンロードしますか？" + e.Message, "JellyParfait - Error", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (msgbox == MessageBoxResult.OK) {
                        await Task.Run(() => GetVideoObject(youtubeUrl));
                    }
                });
                return null;
            } catch (ArgumentException e) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\nURLの形式が間違っています。\n" + e.Message, "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            } catch (AggregateException e) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\nYoutubeのURLかどうかを確認してください\n" + e.Message, "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            }/* catch {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\n不明なエラーが発生しました。\nURLが正しいか確認した後もう一度やり直してください", "JellyParfait", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            }*/
        }

        public async void PlayMusic(MusicData data) {
            Complete = false;
            await Task.Run(() => {
                if (player != null) {
                    player.Dispose();
                    media.Dispose();
                }
            });

            PlayButton.Content = Resources["Pause"];
            data.Visibility = Visibility.Visible;
            data.Color = "NavajoWhite";
            ReloadListView();

            if (data.Thumbnails != null) {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(data.Thumbnails, UriKind.RelativeOrAbsolute);
                bi.EndInit();
                MusicQueueBackground.ImageSource = bi;
            } else {
                MusicQueueBackground.ImageSource = null;
            }
            var volume = VolumeSlider.Value;

            await Task.Run(() => {
                try {
                    player = new WaveOutEvent() { DesiredLatency = 200 };
                    media = new AudioFileReader(data.Url);
                    player.Init(new Equalizer(media, bands));
                    player.Volume = (float)(volume * 0.025);
                    Dispatcher.Invoke(() => {
                        ResetTime();
                        SetSliderTimeLabel(media.TotalTime);
                        ChangeTitle(queue[nowQueue].Title);
                    });
                    var time = new TimeSpan(0, 0, 0);
                    player.Play();
                    var discord_title = data.Title.Length > 40 ? data.Title[0..40] : data.Title;
                    if (_settings.config.DiscordActivity) {
                        _discordClient.SetPresence(new RichPresence() {
                            Details = discord_title,
                            State = "♫再生中♫",
                            Timestamps = Timestamps.Now,
                            Assets = new Assets() {
                                LargeImageKey = "jellyparfait",
                            }
                        });
                    }
                    Complete = true;
                    while (true) {
                        Thread.Sleep(500);
                        try {
                            if (player == null) break;
                            if (media == null) break;
                            if (doPlay) continue;
                            if (player.PlaybackState == PlaybackState.Paused) {
                                if (!doPlay && sliderClick) {
                                    sliderClick = false;
                                    player.Play();
                                }
                                continue;
                            }
                            if (player.PlaybackState == PlaybackState.Stopped) break;

                            if (time != media.CurrentTime) {
                                Dispatcher.Invoke(() => SetTime(media.CurrentTime));
                                time = media.CurrentTime;
                            }

                        } catch (Exception) {
                            sliderClick = false;
                            return;
                        }
                    }
                } catch (System.Runtime.InteropServices.COMException) {
                    Complete = true;
                    sliderClick = false;
                    Dispatcher.Invoke(() => MessageBox.Show("「" + data.Title + "」\n再生エラーが発生しました。\nファイルが破損している可能性があります。キャッシュを消してもう一度試してみてください。", "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
            sliderClick = false;
            if (!Clicked && player != null) {
                if (player.PlaybackState != PlaybackState.Paused) Next();
            }
        }

        public bool IsPlay() {
            if (player == null) return false;
            return player.PlaybackState == PlaybackState.Playing;
        }

        public void Play() {
            if (player != null) {
                player.Play();
                if (_settings.config.DiscordActivity) {
                    var discord_title = queue[nowQueue].Title.Length > 40 ? queue[nowQueue].Title[0..40] : queue[nowQueue].Title;
                    _discordClient.SetPresence(new RichPresence() {
                        Details = discord_title,
                        State = "♫再生中♫",
                        Timestamps = Timestamps.Now,
                        Assets = new Assets() {
                            LargeImageKey = "jellyparfait",
                        }
                    });
                }
                PlayButton.Content = Resources["Pause"];
            }
        }

        public void Stop() {
            if (player != null) {
                player.Stop();
                PlayButton.Content = Resources["Play"];
            }
        }

        public void Reset() {
            queue.Clear();
            if (player != null) {
                player.Dispose();
                player = null;
            }
            if (media != null) {
                media.Dispose();
                media = null;
            }
            MusicQueueBackground.ImageSource = null;
            ChangeTitle(string.Empty);
            ResetTime();
            ResetRichPrecense();
            PlayButton.Content = Resources["Play"];
            ReloadListView();
            nowQueue = -1;
        }

        public void PlayerDispose() {
            if (player != null) {
                player.Dispose();
                player = null;
            }
            if (media != null) {
                media.Dispose();
                media = null;
            }
            MusicQueueBackground.ImageSource = null;
            ChangeTitle(string.Empty);
            ResetTime();
            ResetRichPrecense();
        }

        public void Pause() {
            if (player != null) {
                player.Pause();
                if (_settings.config.DiscordActivity) {
                    var discord_title = queue[nowQueue].Title.Length > 40 ? queue[nowQueue].Title[0..40] : queue[nowQueue].Title;
                    _discordClient.SetPresence(new RichPresence() {
                        Details = discord_title,
                        State = "一時停止中",
                        Timestamps = Timestamps.Now,
                        Assets = new Assets() {
                            LargeImageKey = "jellyparfait",
                        }
                    });
                }
                PlayButton.Content = Resources["Play"];
            }
        }

        private async void Prev() {
            if (Clicked) return;
            if (queue.Count == 0) return;
            Clicked = true;
            PlayerDispose();
            queue[nowQueue].Visibility = Visibility.Hidden;
            queue[nowQueue].Color = "White";
            ReloadListView();
            if (nowQueue == 0) {
                nowQueue = queue.Count - 1;
            } else {
                nowQueue--;
            }
            PlayMusic(queue[nowQueue]);
            await Task.Run(() => {
                while (!Complete) {
                    Thread.Sleep(100);
                }
            });
            Clicked = false;
        }

        private async void Next() {
            if (Clicked) return;
            if (nowQueue == -1) return;
            if (queue.Count == 0) return;
            Clicked = true;

            if (Loop_Button.IsChecked == true || queue.Count == 1) {
                PlayerDispose();
                PlayMusic(queue[nowQueue]);
                await Task.Run(() => {
                    while (!Complete) {
                        Thread.Sleep(100);
                    }
                });
                Clicked = false;
                return;
            }

            if (Shuffle_Button.IsChecked == true) {
                if (queue.Count > 1) {
                    while (true) {
                        var rand = new Random().Next(0, queue.Count);
                        if (rand != nowQueue) {
                            Clicked = false;
                            SetQueue(rand);
                            return;
                        }
                    }
                }
            }

            queue[nowQueue].Visibility = Visibility.Hidden;
            queue[nowQueue].Color = "White";
            ReloadListView();


            if (queue.Count <= nowQueue + 1) {
                nowQueue = 0;
            } else {
                nowQueue++;
            }

            PlayMusic(queue[nowQueue]);

            await Task.Run(() => {
                while (!Complete) {
                    Thread.Sleep(100);
                }
            });

            Clicked = false;
        }

        private void ChangeTitle(string musicTitle) {
            titleLabel.Content = "Now Playing : " + musicTitle;
        }

        private void ReloadListView() {
            MusicQueue.Items.Refresh();
        }

        private void SetTime(TimeSpan time) {
            var hours = "";
            var totalSec = 0;
            var minutes = time.Minutes.ToString();
            if (time.Hours != 0) {
                hours = time.Hours.ToString() + ":";
                totalSec = totalSec + (time.Hours * 60 * 60);
                if (time.Minutes < 10) minutes = "0" + minutes;
            }
            var seconds = time.Seconds.ToString();
            if (time.Seconds < 10) seconds = "0" + seconds;
            totalSec = totalSec + (time.Minutes * 60) + time.Seconds;
            startLabel.Content = hours + minutes + ":" + seconds;
            MusicTimeSlider.Value = totalSec;
        }

        private void ResetTime() {
            startLabel.Content = "0:00";
            endLabel.Content = "0:00";
            MusicTimeSlider.Value = 0;
            MusicTimeSlider.Minimum = 0;
            MusicTimeSlider.Maximum = 1;
        }

        private void SetSliderTimeLabel(TimeSpan totalTime) {
            var hours = "";
            var totalSec = 0;
            var seconds = totalTime.Seconds.ToString();
            var minutes = totalTime.Minutes.ToString();
            if (totalTime.Hours != 0) {
                hours = totalTime.Hours.ToString() + ":";
                totalSec = totalSec + (totalTime.Hours * 60 * 60);
                if (totalTime.Minutes < 10) minutes = "0" + minutes;

            }

            if (totalTime.Seconds < 10) seconds = "0" + seconds;
            endLabel.Content = hours + minutes + ":" + seconds;
            totalSec = totalSec + (totalTime.Minutes * 60) + totalTime.Seconds;
            MusicTimeSlider.Value = 0;
            MusicTimeSlider.Maximum = totalSec;
        }

        public async void SetQueue(int num) {
            if (Clicked) return;
            if (IsPlay()) Stop();
            Clicked = true;
            if (nowQueue != -1) {
                if (nowQueue != num) {
                    queue[nowQueue].Visibility = Visibility.Hidden;
                    queue[nowQueue].Color = "White";
                    ReloadListView();
                }
                PlayerDispose();
            }
            nowQueue = num;
            PlayMusic(queue[num]);
            await Task.Run(() => {
                while (!Complete) {
                    Thread.Sleep(100);
                }
            });
            Clicked = false;
        }

        //MusicData.cs
        public int getQueueId() {
            return nowQueue;
        }

        public void changeClickedFlag(bool flag) {
            Clicked = flag;
        }

        public bool getClickedFlag() {
            return Clicked;
        }

        public void UpMusic(int queueId) {
            if (queueId == 0) return;
            Clicked = true;
            var source = queue[queueId];
            var destination = queue[queueId - 1];
            if (source.QueueId == nowQueue) {
                nowQueue--;
            } else if (destination.QueueId == nowQueue) {
                nowQueue++;
            }
            source.QueueId = queueId - 1;
            destination.QueueId = queueId;
            queue[queueId - 1] = source;
            queue[queueId] = destination;
            ReloadListView();
            Clicked = false;
        }

        public void DownMusic(int queueId) {
            if (queueId == queue.Count - 1) return;
            Clicked = true;
            var source = queue[queueId];
            var destination = queue[queueId + 1];
            if (source.QueueId == nowQueue) {
                nowQueue++;
            } else if (destination.QueueId == nowQueue) {
                nowQueue--;
            }
            source.QueueId = queueId + 1;
            destination.QueueId = queueId;
            queue[queueId + 1] = source;
            queue[queueId] = destination;
            ReloadListView();
            Clicked = false;
        }

        public void DisposeMusicFromQueue(int queueId) {
            Clicked = true;
            var count = 0;
            queue.RemoveAt(queueId);
            if (nowQueue == queueId) {
                PlayerDispose();

            }
            foreach (MusicData music in queue) {
                music.QueueId = count;
                count++;
            }
            if (queueId <= nowQueue) {
                nowQueue--;
            }
            ReloadListView();
            Clicked = false;
        }

        public double GetDirectorySize(DirectoryInfo dirInfo) {
            double DirectorySize = 0;
            foreach (FileInfo fi in dirInfo.GetFiles()) {
                DirectorySize += fi.Length;
            }
            if (DirectorySize != 0) {
                DirectorySize = Math.Round(DirectorySize / 1024 / 1024, 1, MidpointRounding.AwayFromZero);

            }
            return DirectorySize;
        }

        public void ClickExitButtonFromApp() {
            var provider = new MenuItemAutomationPeer(ExitButton) as IInvokeProvider;
            provider.Invoke();
        }

        public EqualizerBand[] GetEqualizer() {
            return bands;
        }

        public void ChangeEqualizer(int index, int value) {
            bands[index].Gain = value;
        }

        private void ResetRichPrecense() {
            if (_settings.config.DiscordActivity) {
                _discordClient.SetPresence(new RichPresence() {
                    Details = "NOT PLAYING",

                    Timestamps = Timestamps.Now,
                    Assets = new Assets() {
                        LargeImageKey = "jellyparfait",
                    }
                });
            }
        }

    }
}
