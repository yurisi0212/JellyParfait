﻿using Microsoft.Win32;
using JellyParfait.Data;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;
using System.Windows.Data;
using System.Windows.Controls;

namespace JellyParfait {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow {

        /// <summary>
        /// フォルダへのパス
        /// </summary>
        private readonly string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\";

        private readonly string cachePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\yurisi\JellyParfait\cache\";

        /// <summary>
        /// 音楽の情報
        /// </summary>
        private MediaFoundationReader media;

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

        private bool download;

        private bool first;

        private MouseButton mouseButton;


        public MainWindow() {
            InitializeComponent();
            if (!Directory.Exists(path)) first = true;
            Directory.CreateDirectory(cachePath);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = true;
            this.Hide();
        }

        private void Window_Closed(object sender, EventArgs e) {
            if (IsPlay()) {
                Stop();
                player.Dispose();
            }
        }

        public void Exit_Click(object sender, RoutedEventArgs e) {
            if (IsPlay()) {
                Stop();
                player.Dispose();
            }
            Application.Current.Shutdown();
        }

        public async void Version_Infomation_Click(object sender, RoutedEventArgs e) {
            await this.ShowMessageAsync("JellyParfait","JellyParfait version 0.9β\n\nCopylight(C)2021 yurisi\nAll rights reserved.\n\n本ソフトウェアはオープンソースソフトウェアです。\nGPL-3.0 Licenseに基づき誰でも複製や改変ができます。\n\nGithub\nhttps://github.com/yurisi0212/JellyParfait"); ;
        }

        private void Mp3_Click(object sender, RoutedEventArgs e) {
            var open = new OpenFileDialog() {
                Title = "mp3ファイルの選択",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter ="mp3ファイル" + "|*.mp3",
                Multiselect = false
            };
            if (open.ShowDialog() != true) return;
            queue.Add(new MusicData(this) {
                QueueId = queue.Count,
                Title = Path.GetFileNameWithoutExtension(open.FileName),
                Id = "local",
                Url = open.FileName,
                YoutubeUrl = open.FileName,
                Thumbnails = null,
                Visibility = Visibility.Hidden,
                Color = "white",
            });
            ReloadListView();
            if (queue.Count == 1) {
                nowQueue = 0;
                PlayMusic(queue[nowQueue]);
            }

        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) Search();
        }

        private async void SearchTextBox_Loaded(object sender, RoutedEventArgs e) {
            //if (first) {
                var settings = new MetroDialogSettings {
                    DefaultText = "https://www.youtube.com/watch?list=PL1kIh8ZwhZzKMU8MELWCfveQifBZWUIhi",
                };
                var dialog = await this.ShowInputAsync("ようこそJellyParfaitへ！", "youtubeのURLを入力してください！(プレイリストでもOK)\n初回はキャッシュのダウンロードがあるので時間がかかります", settings);
                if (dialog == null) return;
                if (dialog == String.Empty) return;
                searchTextBox.Text = dialog;
                Search();
            //}
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
            myBinding.NotifyOnSourceUpdated = true;
            BindingOperations.SetBinding(MusicQueue, ItemsControl.ItemsSourceProperty, myBinding);
        }

        private void MusicTimeSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            if (player != null) {
                Pause();
                if (player.PlaybackState == PlaybackState.Paused) {
                    media.Position = (long)(media.WaveFormat.AverageBytesPerSecond * Math.Floor(MusicTimeSlider.Value));
                    Play();
                }
            }
            sliderClick = false;
        }

        private void MusicTimeSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            sliderClick = true;
            if (player == null) {
                MusicTimeSlider.Value = 0;
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
                player.Volume = (float)VolumeSlider.Value;
            }
        }

        private void moveProgressBar(int value) {
            Dispatcher.Invoke(() => Progress.Value = value );
        }

        private void Search() {
            if (Searched == searchTextBox.Text) {
                var msgbox = MessageBox.Show(this, "現在検索しているようです。もう一度追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (msgbox == MessageBoxResult.No) return;
            }
            CheckURL(searchTextBox.Text);
        }

        private async void CheckURL(string youtubeUrl) {
            Searched = youtubeUrl;
            Progress.Visibility = Visibility.Visible;
            try {
                var youtube = new YoutubeClient();
                var playlist = await youtube.Playlists.GetAsync(youtubeUrl);
                var videos = youtube.Playlists.GetVideosAsync(playlist.Id);
                await foreach (var video in videos) {
                    if (queue.Exists(x => x.YoutubeUrl == video.Url)) {
                        var msgbox = MessageBox.Show(this, video.Title + "\n既に存在しているようです。追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (msgbox == MessageBoxResult.No) continue;
                    }
                    await Task.Run(() => AddQueue(video.Url));
                }

            } catch (ArgumentException) {
                if (queue.Exists(x => x.YoutubeUrl == youtubeUrl)) {
                    var msgbox = MessageBox.Show(this ,"既に存在しているようです。追加しますか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (msgbox == MessageBoxResult.No) return;
                }
                await Task.Run(() => AddQueue(youtubeUrl));
            } finally {
                Searched = String.Empty;
                Progress.Visibility = Visibility.Hidden;
            }
        }

        private void AddQueue(string youtubeUrl) {
            moveProgressBar(0);
            try {
                MusicData musicData = null;
                musicData =  GetVideoObject(youtubeUrl).Result;
                if (musicData == null) return;
                if (musicData.Url == string.Empty) return;

                queue.Add(musicData);
                Dispatcher.Invoke(() => ReloadListView());

                if (queue.Count == 1) {
                    nowQueue = 0;
                    Dispatcher.Invoke(() => PlayMusic(musicData));
                }
            } catch (AggregateException) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\n有効な動画ではありませんでした。(ライブ配信は対応していません。)", "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }

        private async Task<MusicData> GetVideoObject(string youtubeUrl) {
            try {
                moveProgressBar(1);
                var youtubeClient = new YoutubeClient();
                var video = await youtubeClient.Videos.GetAsync(youtubeUrl);
                var music = cachePath + video.Id + ".mp3";
                var image = cachePath + video.Id + ".jpg";
                moveProgressBar(2);

                if (!File.Exists(music)) {
                    if (TimeSpan.Compare(video.Duration.Value, new TimeSpan(0, 30, 0)) == 1) {
                        var result = false;
                        Dispatcher.Invoke(() => {
                            var msgbox = MessageBox.Show(this, "「" + video.Title + "」\n" + "この動画は30分を超えています。\nダウンロードに時間がかかり、空き容量を多く使う可能性がありますがよろしいでしょうか？", "JellyParfait", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (msgbox == MessageBoxResult.No) result = true;
                        });
                        if (result) return null;
                    }
                    var manifest = await youtubeClient.Videos.Streams.GetManifestAsync(video.Id);
                    var info = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    await youtubeClient.Videos.Streams.DownloadAsync(info, music);
                }

                moveProgressBar(3);
                if (!File.Exists(image)) {
                    using WebClient client = new WebClient();
                    await Task.Run(() => client.DownloadFile(new Uri("https://img.youtube.com/vi/" + video.Id + "/maxresdefault.jpg"), image));
                }

                moveProgressBar(4);
                return new MusicData(this) {
                    QueueId = queue.Count,
                    Title = video.Title,
                    Id = video.Id,
                    Url = music,
                    YoutubeUrl = youtubeUrl,
                    Thumbnails = image,
                    Visibility = Visibility.Hidden,
                    Color = "white",
                }; ;
            } catch (System.Net.Http.HttpRequestException) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\nインターネットに接続されているか確認してください", "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            } catch (ArgumentException) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\nURLの形式が間違っています。", "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return null;
            } catch (AggregateException) {
                Dispatcher.Invoke(() => MessageBox.Show(this, "Error\nYoutubeのURLかどうかを確認してください", "JellyParfait - Error", MessageBoxButton.OK, MessageBoxImage.Warning));
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

            var volume = (float)VolumeSlider.Value;

            await Task.Run(() => {
                player = new WaveOutEvent() { DesiredLatency = 200 };
                media = new MediaFoundationReader(data.Url) {
                    Position = 0
                };
                player.Init(media);
                player.Volume = volume;
                ;
                Dispatcher.Invoke(() => {
                    ResetTime();
                    SetSliderTimeLabel(media.TotalTime);
                    ChangeTitle(queue[nowQueue].Title);
                });

                var time = new TimeSpan(0, 0, 0);

                player.Play();

                Complete = true;

                while (true) {
                    Thread.Sleep(200);
                    if (player == null) break;
                    if (player.PlaybackState == PlaybackState.Paused) continue;
                    if (player.PlaybackState == PlaybackState.Stopped) break;
                    if (sliderClick) continue;
                    if (time != media.CurrentTime) {
                        Dispatcher.Invoke(() => SetTime(media.CurrentTime));
                        time = media.CurrentTime;
                    }
                }
            });

            if (!Clicked) {
                if (player.PlaybackState != PlaybackState.Paused) Next();
            }

            if (nowQueue != data.QueueId) {
                data.Visibility = Visibility.Hidden;
                data.Color = "White";
                ReloadListView();
            }
        }

        public bool IsPlay() {
            if (player == null) return false;
            return player.PlaybackState == PlaybackState.Playing;
        }

        public void Play() {
            if (player != null) {
                player.Play();
                PlayButton.Content = Resources["Pause"];
            }
        }

        public void Stop() {
            if (player != null) {
                player.Stop();
                PlayButton.Content = Resources["Play"];
            }
        }

        public void PlayerDispose() {
            player.Dispose();
            ChangeTitle(string.Empty);
            ResetTime();
        }

        public void Pause() {
            if (player != null) {
                player.Pause();
                PlayButton.Content = Resources["Play"];
            }
        }

        private async void Prev() {
            if (Clicked) return;
            if (queue.Count == 0) return;
            Clicked = true;
            PlayerDispose();
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
            nowQueue = num;
            PlayerDispose();
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

    }
}
