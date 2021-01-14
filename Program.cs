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

            var mentionOptons = new ListTweetsMentioningMeOptions();

            var newMentions = service.ListTweetsMentioningMe(mentionOptons).ToList();

            if (!newMentions.Any()) return;


            foreach (var mention in newMentions)
            {
                if (mention.IsFavorited)
                {
                    continue;
                }

                if (mention.Entities.Media.Any())
                {
                    status = "Hey! There is no Dynamo graph image in this tweet. Much sad.";
                }

                string classificationImage = ClassifyImage(mention.Entities.Media.First().MediaUrl);

                switch (classificationImage)
                {
                    case "Yes":
                        status = "nice job this graph has annotations";
                        break;
                    case "No":
                        status = "this graph has no annotations and makes me sad.";
                        break;
                }

                //favorite the tweet so we don't do it again
                FavoriteTweetOptions favoriteOptions = new FavoriteTweetOptions { Id = mention.Id };
                service.FavoriteTweet(favoriteOptions);

                var result = service.SendTweet(new SendTweetOptions
                {
                    Status = status,
                    InReplyToStatusId = mention.Id,
                    AutoPopulateReplyMetadata = true
                });
            }

        }



        private static string ClassifyImage(string imageUrl)
        {
            var signatureFilePath = Configuration["sig_file_path"]; ;

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(imageUrl), @"c:\temp\image.png");
            }

            ImageClassifier.Register("onnx", () => new OnnxImageClassifier());
            using var classifier = ImageClassifier.CreateFromSignatureFile(
                new FileInfo(signatureFilePath));

            var results = classifier.Classify(Image
                .Load(@"c:\temp\image.png").CloneAs<Rgb24>());
            Console.WriteLine(results.Classifications.First().Label);

            return results.Classifications.First().Label;
        }
    }
}
