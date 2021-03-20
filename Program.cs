using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace KhinsiderDownloader
{
  class Song
  {
    public string Name { get; set; }
    public Uri PageUri { get; set; }
  }

  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length != 2)
      {
        throw new Exception("Missing arguments!");
      }

      Console.WriteLine($"From: {args.First()}");
      Console.WriteLine($"To: {args.Skip(1).First()}");
      Console.WriteLine();

      if (!Directory.Exists(args[1]))
      {
        Directory.CreateDirectory(args[1]);
      }

      GetSongList(new Uri(args[0]))
          .ContinueWith(t =>
          {

            foreach (var song in t.Result)
            {
              Console.WriteLine($"Song:       {song.Name}");
              Console.WriteLine($"Page URI:   {song.PageUri}");
              Console.WriteLine($"Local File: {DownloadSong(args[1], song).Result}");
              Console.WriteLine();
            }
          })
          .Wait();
    }

    static async Task<string> DownloadSong(string destination, Song song)
    {
      using (var client = new HttpClient())
      {
        var response = await client.GetAsync(song.PageUri);
        var document = new HtmlDocument
        {
          OptionAutoCloseOnEnd = true
        };

        document.LoadHtml(response.Content.ReadAsStringAsync().Result);

        var songUri = new Uri(document.DocumentNode.Descendants("audio").First().Attributes["src"].Value);
        var songFileName = HttpUtility.UrlDecode(songUri.PathAndQuery.Substring(songUri.PathAndQuery.LastIndexOf('/') + 1));
        var songPath = Path.Combine(destination, songFileName);

        var songResponse = await client.GetAsync(songUri);

        using (var fs = File.Create(songPath))
        {
            await songResponse.Content.CopyToAsync(fs);
        }

        return songPath;
      }
    }


    static async Task<IEnumerable<Song>> GetSongList(Uri uri)
    {
      using (var client = new HttpClient())
      {
        var response = await client.GetAsync(uri);
        var document = new HtmlDocument
        {
          OptionAutoCloseOnEnd = true
        };

        document.LoadHtml(response.Content.ReadAsStringAsync().Result);

        var songList = document.DocumentNode
                               .Descendants("table")
                               .First(table => table.Attributes["id"] != null &&
                                               table.Attributes["id"].Value == "songlist")
                               .Descendants("td")
                               .Where(td => td.Attributes.Count == 1 &&
                                            td.Attributes["class"] != null &&
                                            td.Attributes["class"].Value == "clickable-row")
                               .Select(td => td.Element("a"))
                               .Select(a => new Song
                               {
                                 Name = a.InnerText,
                                 PageUri = new Uri(uri, a.Attributes["href"].Value)
                               });

        return songList;
      }
    }
  }
}
