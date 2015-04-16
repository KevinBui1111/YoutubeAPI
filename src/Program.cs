using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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
            Console.WriteLine("YouTube Data API: Search");
            Console.WriteLine("========================");

            try
            {
                var search = new Program();
                var vids = search.GetUploadVids();
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        YouTubeService youtubeService;
        List<string> vidInfos;

        public Program()
        {
            youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCrv5oNZ5x8TfIpQ5yeaoz1VSBNPwRQdqg",
                ApplicationName = this.GetType().ToString()
            });
            vidInfos = new List<string>();
        }

        private string[] GetUploadVids()
        {
            var begin = DateTime.Now;

            //return null;
            List<string> vids = new List<string>();

            var channelsListRequest = youtubeService.Channels.List("contentDetails");
            channelsListRequest.Id = "UCuN9yuqJWM6CeqqUKPFckhA";
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

                nextPageToken = playlistItemsListResponse.NextPageToken;
            }

            Console.WriteLine("Total {0} videos, distinct {1}", vids.Count, vids.Distinct().Count());

            File.WriteAllText("vid list.txt", string.Join("\r\n", vids));

            Console.WriteLine((DateTime.Now - begin).TotalSeconds);

            begin = DateTime.Now;
            Task.WaitAll(tasklist.ToArray());
            Console.WriteLine((DateTime.Now - begin).TotalSeconds);

            File.WriteAllText("view count.txt", string.Join("\r\n", vidInfos));

            return vids.ToArray();
        }
        private IEnumerable<string> GetVidsInfo(IEnumerable<string> vids)
        {
            List<string> vidcount = new List<string>();

            var playlistItemsListRequest = youtubeService.Videos.List("statistics,snippet");
            playlistItemsListRequest.Id = string.Join(",", vids);

            // Retrieve the list of videos uploaded to the authenticated user's channel.
            var playlistItemsListResponse = playlistItemsListRequest.Execute();

            return playlistItemsListResponse.Items.Select(p => p.Id + "\t" + p.Statistics.ViewCount + "\t" + p.Snippet.Title);
        }

        private void completeGetVidInfo(IEnumerable<string> vids)
        {
            vidInfos.AddRange(vids);
        }
    }
}
