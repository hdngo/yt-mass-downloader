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
using System.IO;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using MaterialDesignThemes.Wpf;
using YoutubeExplode.Converter;
using YoutubeSearch;
using System.Web;

namespace YoutubeDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static YoutubeClient client = new YoutubeClient();
        public static BrushConverter brushConverter = new BrushConverter();
        public static YoutubeConverter youtubeConverter = new YoutubeConverter(client);
        public static VideoSearch videoSearch = new VideoSearch();

        public MainWindow()
        {
            InitializeComponent();

            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            DirectoryText.Text = path;

        }

        private async void ImportPlaylist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var playlistId = YoutubeClient.ParsePlaylistId(GetPlaylistLink());
                var playlist = await client.GetPlaylistAsync(playlistId);

                SetProgressText("Importing Playlist...");
                SetProgressValue(0, playlist.Videos.Count());
                ToggleProgress();

                var count = 1;
                foreach (var video in playlist.Videos)
                {
                    AddLinkToTextBox("\n" + "https://www.youtube.com/watch?v=" + video.Id);
                    count++;
                }

                ToggleProgress();
                SetProgressValue(0);
                SetProgressText(String.Empty);
            }
            catch { MessageBox.Show("Error: Invalid playlist ID!"); }
        }
        private string GetPlaylistLink() => PlaylistLinkBox.Text;
        private void AddLinkToTextBox(string link)
        {
            VideoLinkBox.Text += link;
        }

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog() {
                Description = "Select a folder",
                UseDescriptionForTitle = true
            };
            if ((bool)dialog.ShowDialog(this)) DirectoryText.Text = dialog.SelectedPath;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            var count = 0;
            var errorCount = 0;
            var downloadLinks = GetDownloadLinks();
            SetProgressText($"{count} / {downloadLinks.Count()}");
            SetProgressValue(0, downloadLinks.Count());
            foreach (var line in downloadLinks)
            {
                try
                {
                    var id = YoutubeClient.ParseVideoId(line);
                    var streamInfoSet = await client.GetVideoMediaStreamInfosAsync(id);
                    var formatInt = GetFormat();
                    var qualityInt = GetQuality();

                    if (formatInt == 0)
                    {
                        VideoStreamInfo vidStreamInfo;
                        var videoStreamInfoList = streamInfoSet.Video.Where(x => x.Container == Container.Mp4).OrderByDescending(x => x.VideoQuality);
                        AudioStreamInfo audioStreamInfo;
                        var audioStreamInfoList = streamInfoSet.Audio.Where(x => x.Container == Container.Mp4).OrderByDescending(x => x.Bitrate);

                        if (qualityInt == 0) { vidStreamInfo = videoStreamInfoList.First(); audioStreamInfo = audioStreamInfoList.First(); }
                        else if (qualityInt == 2) { vidStreamInfo = videoStreamInfoList.Last(); audioStreamInfo = audioStreamInfoList.Last(); }
                        else { vidStreamInfo = videoStreamInfoList.ElementAt((int)(videoStreamInfoList.Count() / 2)); audioStreamInfo = audioStreamInfoList.ElementAt((int)(audioStreamInfoList.Count() / 2)); }

                        var mediaStreamInfos = new MediaStreamInfo[] { audioStreamInfo, vidStreamInfo };
                        await youtubeConverter.DownloadAndProcessMediaStreamsAsync(mediaStreamInfos, $"{GetFilePath()}\\{ReturnValidFileName(client.GetVideoAsync(id).Result.Title)}.mp4", "mp4");
                    }
                    else if (formatInt == 1)
                    {
                        AudioStreamInfo streamInfo;
                        var streamInfoList = streamInfoSet.Audio.Where(x => x.Container == Container.Mp4).OrderByDescending(x => x.Bitrate);

                        if (qualityInt == 0) streamInfo = streamInfoList.First();
                        else if (qualityInt == 2) streamInfo = streamInfoList.Last();
                        else streamInfo = streamInfoList.ElementAt((int)(streamInfoList.Count() / 2));

                        var mediaStreamInfos = new MediaStreamInfo[] { streamInfo };
                        await youtubeConverter.DownloadAndProcessMediaStreamsAsync(mediaStreamInfos, $"{GetFilePath()}\\{ReturnValidFileName(client.GetVideoAsync(id).Result.Title)}.mp3", "mp3");
                    }
                    else
                    {
                        VideoStreamInfo streamInfo;
                        var streamInfoList = streamInfoSet.Video.Where(x => x.Container == Container.Mp4).OrderByDescending(x => x.VideoQuality);

                        if (qualityInt == 0) streamInfo = streamInfoList.First();
                        else if (qualityInt == 2) streamInfo = streamInfoList.Last();
                        else streamInfo = streamInfoList.ElementAt((int)(streamInfoList.Count() / 2));

                        var mediaStreamInfos = new MediaStreamInfo[] { streamInfo };
                        await youtubeConverter.DownloadAndProcessMediaStreamsAsync(mediaStreamInfos, $"{GetFilePath()}\\{ReturnValidFileName(client.GetVideoAsync(id).Result.Title)}.mp4", "mp4");
                    }
                }
                catch { errorCount++; continue;  }
                count++;
                SetProgressText($"{count} / {GetDownloadLinks().Count()}");
                SetProgressValue(count);
            }
            MessageBox.Show($"Download finished with {errorCount} errors!");
            DownloadButton.IsEnabled = true;
            SetProgressText(String.Empty);
            SetProgressValue(0);
        }

        private string ReturnValidFileName(string fileName)
        {
            var invalid = new string(System.IO.Path.GetInvalidFileNameChars());
            foreach (var c in invalid) fileName = fileName.Replace(c.ToString(), "");
            return fileName;
        }
        private string GetFilePath() => DirectoryText.Text;
        private int GetQuality() => QualityAccepted.SelectedIndex;
        private int GetFormat() => FormatAccepted.SelectedIndex;
        private string[] GetDownloadLinks() => VideoLinkBox.Text.Trim().Split(new[] { "\n" }, StringSplitOptions.None);

        private void SetProgressText(string text) => ProgressText.Text = text;
        private void SetProgressValue(int value, int? maxValue = null)
        {
            if (maxValue == null) DownloadProgressBar.Value = value;
            else
            {
                DownloadProgressBar.Maximum = Convert.ToDouble(maxValue);
                DownloadProgressBar.Value = value;
            }
        }
        private void ToggleProgress()
        {
            if (DownloadProgressBarBehind.IsIndeterminate) DownloadProgressBarBehind.IsIndeterminate = false;
            else DownloadProgressBarBehind.IsIndeterminate = true;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = GetSearchText();
            ToggleProgress();
            if (!String.IsNullOrEmpty(searchQuery))
            {
                try
                {
                    var results = await videoSearch.SearchQueryTaskAsync(GetSearchText(), 1);
                    ClearListBox();

                    foreach (var result in results)
                        AddListBox(result.Title.Length > 40 ? result.Title.Substring(0, 40) + "..." : result.Title, result.Url, result.Thumbnail, result.Duration);
                }
                catch (Exception err) { MessageBox.Show($"Search not found! Check proxy/internet and try again!\n{err.Message}\n{err.StackTrace}"); ToggleProgress(); return; }
            }
            ToggleProgress();
        }

        private string GetSearchText() => SearchText.Text;
        private void ClearListBox() => VideoSearchBox.Items.Clear();
        private void AddListBox(string title, string url, string thumbnailUrl, string duration)
        {
            var importButton = new Button()
            {
                Content = new PackIcon()
                {
                    Kind = PackIconKind.Import,
                    Width = 20,
                    Height = 20
                },
                Margin = new Thickness(0,0,40,0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Height = 30,
                Width = 60,
                Name = "a" + url.Replace("http://www.youtube.com/watch?v=", String.Empty).Replace("-", "CONNECTOR")
            };
            importButton.Click += ImportLink;

            var titleAndUrlStackPanel = new StackPanel() 
            { 
                Margin = new Thickness(10,0,0,0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleText = new TextBlock() { Text = $"{HttpUtility.HtmlDecode(title)} [{duration}]" };
            var urlText = new TextBlock() { Text = url, Foreground = (Brush)brushConverter.ConvertFromString("#FF898989") };
            titleAndUrlStackPanel.Children.Add(titleText);
            titleAndUrlStackPanel.Children.Add(urlText);

            var image = new Image() { Source = new BitmapImage(new Uri(thumbnailUrl)) };
            var mainStackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            mainStackPanel.Children.Add(image);
            mainStackPanel.Children.Add(titleAndUrlStackPanel);

            var grid = new Grid() { Width = VideoSearchBox.ActualWidth };
            grid.Children.Add(mainStackPanel);
            grid.Children.Add(importButton);

            VideoSearchBox.Items.Add(new ListBoxItem() { Height =  70, Content = grid });
        }

        private void ImportLink(object sender, RoutedEventArgs e)
        {
            AddLinkToTextBox("https://www.youtube.com/watch?v=" + (sender as Button).Name.Substring(1).Replace("CONNECTOR", "-") + "\n");
        }

        private void SearchText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SearchButton_Click(sender, e);
        }
    }
}
