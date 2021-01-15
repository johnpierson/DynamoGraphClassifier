using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using lobe;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using lobe.ImageSharp;
using Microsoft.Extensions.Configuration;
using TweetSharp;

namespace ClassifyDynamoGraph
{
    class Program
    {
        private static readonly IConfiguration Configuration =
            new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
        private static readonly string[] Compliments = new[]
        {
            "awesome!",
            "well lookie here",
            "nice job!"
        };
        private static readonly string[] Bummer = new[]
        {
            "oh no.",
            "bummer.",
            "darn."
        };

        static void Main(string[] args)
        {
            
            string status = string.Empty;

            var service = new TwitterService(
                Configuration["twitter_consumer_key"],
                Configuration["twitter_consumer_secret"]
            );

            service.AuthenticateWith(
                Configuration["twitter_access_token"],
                Configuration["twitter_access_token_secret"]
            );

            var mentionOptions = new ListTweetsMentioningMeOptions();

            //only get mentions within the last day
            var newestMentions = service.ListTweetsMentioningMe(mentionOptions).Where(m => (DateTime.Now - m.CreatedDate).Days <= 1).ToList();
          
            //get out if there are none
            if (!newestMentions.Any()) return;

            int imageFlag = 0;

            foreach (var mention in newestMentions)
            {
                //we favorite the mention to skip it if we run it again and find it
                if (mention.IsFavorited)
                {
                    continue;
                }

                if (!mention.Entities.Media.ToList().Any())
                {
                    status = "Hey! There is no Dynamo graph image in this tweet. Much sad.";
                }

                else
                {
                    string classificationImage = ClassifyImage(mention.Entities.Media.First().MediaUrl, imageFlag);
                    var random = new Random();
                    int index = random.Next(Compliments.Length);
                    switch (classificationImage)
                    {
                        case "Yes":
                            status = $"{Compliments[index]} this graph has annotations";
                            break;
                        case "No":
                            status = $"{Bummer[index]} this graph has no annotations and makes me sad.";
                            break;
                    }
                }
              

                //favorite the tweet so we don't do it again
                FavoriteTweetOptions favoriteOptions = new FavoriteTweetOptions { Id = mention.Id };
                service.FavoriteTweet(favoriteOptions);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var result = service.SendTweet(new SendTweetOptions
                    {
                        Status = status,
                        InReplyToStatusId = mention.Id,
                        AutoPopulateReplyMetadata = true
                    });
                }
              
            }
        }

        private static string ClassifyImage(string imageUrl, int imageFlag)
        {
            string imageName = $"c:\\temp\\image{imageFlag}.png";
            var signatureFilePath = Configuration["sig_file_path"]; ;

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(imageUrl), imageName);
            }

            ImageClassifier.Register("onnx", () => new OnnxImageClassifier());
            using var classifier = ImageClassifier.CreateFromSignatureFile(
                new FileInfo(signatureFilePath));

            var results = classifier.Classify(Image
                .Load(imageName).CloneAs<Rgb24>());
            Console.WriteLine(results.Classifications.First().Label);

            return results.Classifications.First().Label;
        }
    }
}
