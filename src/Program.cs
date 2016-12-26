using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Google.Apis.YouTube.v3.Data;
using YoutubeAPI.Properties;
using System.Net;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Google.Apis.Util.Store;

namespace YoutubeAPI
{
    /// <summary>
    /// YouTube Data API v3 sample: search by keyword.
    /// Relies on the Google APIs Client Library for .NET, v1.7.0 or higher.
    /// See https://code.google.com/p/google-api-dotnet-client/wiki/GettingStarted
    ///
    /// Set ApiKey to the API key value from the APIs & auth > Registered apps tab of
    ///   https://cloud.google.com/console
    /// Please ensure that you have enabled the YouTube Data API for your project.
    /// </summary>
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var search = new Program();
                //search.GetVidsTitle_Wrapper(Resources.vidlist.Split(','));
                search.GetUploadVids();
                //search.GetSubscriptions();
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("Finish. Press any key to continue...");
            Console.ReadKey();
        }

        YouTubeService youtubeService;
        List<Video> vidInfos;
        const string saveFolder = @"D:\Downloads\Video\YDL\_channels\";

        public Program()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                Task<UserCredential> t = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    // This OAuth 2.0 access scope allows for read-only access to the authenticated 
                    // user's account, but not other types of account access.
                    new[] { YouTubeService.Scope.YoutubeReadonly },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("oauth")
                );
                t.Wait();
                credential = t.Result;
            }

            youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                //ApiKey = "AIzaSyCrv5oNZ5x8TfIpQ5yeaoz1VSBNPwRQdqg",
                HttpClientInitializer = credential,
                ApplicationName = this.GetType().ToString()
            });

            vidInfos = new List<Video>();
        }

        List<Channel> channels = new List<Channel>();
        private void GetSubscriptions()
        {
            
            List<Task> tasklist = new List<Task>();

            var nextPageToken = "";
            while (nextPageToken != null)
            {
                var request = youtubeService.Subscriptions.List("snippet");
                request.Mine = true;
                request.MaxResults = 50;
                request.PageToken = nextPageToken;

                var response = request.Execute();
                var listchannel = response.Items.Select(i => new Channel
                {
                    title = i.Snippet.Title,
                    channelId = i.Snippet.ResourceId.ChannelId,
                    thumbnail = i.Snippet.Thumbnails.Default__.Url
                }).ToArray();
                channels.AddRange(listchannel);
                nextPageToken = response.NextPageToken;

                GetChannelInfo(listchannel);
                //tasklist.Add(Task.Factory.StartNew(() => { GetChannelInfo(listchannel); }));
            }

            Task.WaitAll(tasklist.ToArray());

            //channels.Sort((a, b) => b.subscriberCount.CompareTo(a.subscriberCount));

            StringBuilder channel_part = new StringBuilder();
            foreach (var vid in channels)
                channel_part.AppendFormat(Resources.channel_template, vid.title, vid.subscriberCount, vid.viewCount, vid.videoCount, vid.channelId, vid.thumbnail)
                    .AppendLine();

            File.WriteAllText("my_channels.html"
                             , string.Format(Resources.channelwrapper_template, channel_part));

            //foreach (Channel c in channels)
            //    Console.WriteLine("\t{0}\t{1:n0}\t{2:n0}", c.title, c.viewCount, c.subscriberCount);
        }
        private void GetChannelInfo(IEnumerable<Channel> vids)
        {
            List<string> vidcount = new List<string>();

            var request = youtubeService.Channels.List("statistics");
            request.Id = string.Join(",", vids.Select(c => c.channelId));

            // Retrieve the list of videos uploaded to the authenticated user's channel.
            var response = request.Execute();

            var pairs = from s in vids
                       join d in response.Items
                           on s.channelId equals d.Id
                       select new { s, d };

            foreach (var pair in pairs)
            {
                pair.s.videoCount = pair.d.Statistics.VideoCount.Value;
                pair.s.viewCount = pair.d.Statistics.ViewCount.Value;
                pair.s.subscriberCount = pair.d.Statistics.SubscriberCount.Value;
            };
        }

        private void GetUploadVids()
        {
            //==================== INPUT ===========================
            Console.WriteLine("YouTube Data API: Search");
            Console.WriteLine("========================");

            Console.Write("Input channel ID: ");
            var channelID = Console.ReadLine();

            Console.Write("Stop video ID: ");
            var stop_at_vid = Console.ReadLine();
            //=======================================================

            var begin = DateTime.Now;

            //return null;
            List<string> vids = new List<string>();

            var channelsListRequest = youtubeService.Channels.List("contentDetails,snippet");
            channelsListRequest.Id = channelID;
            //channelsListRequest.Mine = true;

            // Retrieve the contentDetails part of the channel resource for the authenticated user's channel.
            var channelsListResponse = channelsListRequest.Execute();

            var channel = channelsListResponse.Items[0];
            // From the API response, extract the playlist ID that identifies the list
            // of videos uploaded to the authenticated user's channel.
            var uploadsListId = channel.ContentDetails.RelatedPlaylists.Uploads;

            Console.WriteLine("Videos in list {0}", uploadsListId);

            List<Task> tasklist = new List<Task>();
            var nextPageToken = "";
            while (nextPageToken != null)
            {
                var playlistItemsListRequest = youtubeService.PlaylistItems.List("contentDetails");
                playlistItemsListRequest.PlaylistId = uploadsListId;
                playlistItemsListRequest.MaxResults = 50;
                playlistItemsListRequest.PageToken = nextPageToken;

                // Retrieve the list of videos uploaded to the authenticated user's channel.
                var playlistItemsListResponse = playlistItemsListRequest.Execute();

                if (nextPageToken == "") Console.WriteLine("Total {0} videos", playlistItemsListResponse.PageInfo.TotalResults);

                var vidIDs = playlistItemsListResponse.Items.Select(p => p.ContentDetails.VideoId);
                vids.AddRange(vidIDs);

                tasklist.Add(
                    Task.Factory.StartNew(list => GetVidsInfo((IEnumerable<string>)list), vidIDs)
                        .ContinueWith(t => completeGetVidInfo(t.Result), TaskScheduler.Current)
                    );

                if (vidIDs.Contains(stop_at_vid))
                    nextPageToken = null;
                else
                    nextPageToken = playlistItemsListResponse.NextPageToken;
            }

            Console.WriteLine("Total {0} videos, distinct {1}", vids.Count, vids.Distinct().Count());

            Task.WaitAll(tasklist.ToArray());

            desFolder = saveFolder + channel.Snippet.Title;
            string thumbFormat = channel.Snippet.Title + "/{0}.jpg";

            StringBuilder htmlpage = new StringBuilder();
            foreach (var vid in vidInfos)
            {
                string thumbUrl = string.Format(thumbFormat, vid.Id);
                htmlpage.AppendFormat(Resources.item_template, thumbUrl, vid.Id, vid.Snippet.Title, vid.Statistics.ViewCount.Value.ToString("#,#"), vid.Snippet.PublishedAt.ToHumanDate())
                    .AppendLine();
            }

            string filename = string.Format(saveFolder + "\\{0}.html", channel.Snippet.Title);
            int num = 1;
            while (File.Exists(filename))
            {
                filename = string.Format(saveFolder + "\\{0}_{1}.html", channel.Snippet.Title, num++);
            }

            File.WriteAllText(filename
                             ,string.Format(Resources.body_template, channel.Id, channel.Snippet.Title, htmlpage.ToString()));

            Console.WriteLine((DateTime.Now - begin).TotalSeconds);

            Console.WriteLine("Downloading thumbnail...");
            begin = DateTime.Now;
            // Download thumbnail
            if (!Directory.Exists(desFolder))
                Directory.CreateDirectory(desFolder);

            var res = Parallel.ForEach(vidInfos, (Action<Video>)DownImage);
            Console.WriteLine((DateTime.Now - begin).TotalSeconds);
        }
        private IEnumerable<Video> GetVidsInfo(IEnumerable<string> vids)
        {
            List<string> vidcount = new List<string>();

            var playlistItemsListRequest = youtubeService.Videos.List("statistics,snippet");
            playlistItemsListRequest.Id = string.Join(",", vids);

            // Retrieve the list of videos uploaded to the authenticated user's channel.
            var playlistItemsListResponse = playlistItemsListRequest.Execute();

            return playlistItemsListResponse.Items;//.Select(p => p.Id + "\t" + p.Statistics.ViewCount + "\t" + p.Snippet.Title);
        }

        private void GetVidsTitle_Wrapper(string[] vids)
        {
            List<Task> tasklist = new List<Task>();
            foreach (var vidpart in vids.Chunkify(50))
            {
                tasklist.Add(
                    Task.Factory.StartNew(list => GetVidsInfoTitle((IEnumerable<string>)list), vidpart)
                        .ContinueWith(t => completeGetVidInfo(t.Result), TaskScheduler.Current)
                    );
            }

            Task.WaitAll(tasklist.ToArray());

            File.WriteAllText("a.txt", string.Join("\r\n", vidInfos.Select(v => string.Format("{0}\t{1}", v.Id, v.Snippet.Title))));
        }
        private IEnumerable<Video> GetVidsInfoTitle(IEnumerable<string> vids)
        {
            List<string> vidcount = new List<string>();

            var playlistItemsListRequest = youtubeService.Videos.List("snippet");
            playlistItemsListRequest.Id = string.Join(",", vids);

            // Retrieve the list of videos uploaded to the authenticated user's channel.
            var playlistItemsListResponse = playlistItemsListRequest.Execute();

            return playlistItemsListResponse.Items;
        }

        private void completeGetVidInfo(IEnumerable<Video> vids)
        {
            vidInfos.AddRange(vids);
        }

        string desFolder;
        const string thumbnailURL = "https://i.ytimg.com/vi/{0}/mqdefault.jpg";
        const string thumbnailFormatPath = "https://i.ytimg.com/vi/{0}/mqdefault.jpg";

        public void DownImage(Video vid)
        {
            var stream = DownloadURL(string.Format(thumbnailURL, vid.Id));
            if (stream == null) return;

            using (Stream destination = File.Create(desFolder + "\\" + vid.Id + ".jpg"))
                stream.CopyTo(destination);
        }
        public Stream DownloadURL(string url)
        {
            try
            {
                WebRequest req = WebRequest.Create(url);
                WebResponse response = req.GetResponse();
                return response.GetResponseStream();
            }
            catch (Exception ex)
            {
                Console.WriteLine(url + " - " + ex.Message);
                return null;
            }
        }
    }

    class Channel
    {
        public string channelId { get; set; }
        public string title { get; set; }
        public string thumbnail { get; set; }
        public ulong videoCount { get; set; }
        public ulong subscriberCount { get; set; }
        public ulong viewCount { get; set; }
    }
}
